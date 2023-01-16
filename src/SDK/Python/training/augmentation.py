# from https://blog.francium.tech/custom-object-detection-with-yolo-v5-7b7bc6d82e45


import torchvision
import albumentations as A
from albumentations.pytorch.transforms import ToTensorV2

class Transform:
    image_size = 512

    def aug_img(self, image):
        raise NotImplementedError
    
    def aug_images(self, images):
        raise NotImplementedError
    
    def deaug_bboxes(self, boxes):
        raise NotImplementedError

class HorizontalFlip(Transform):
    def aug_img(self, image):
        return image.flip(1)
    
    def aug_images(self, images):
        return images.flip(2)
    
    def deaug_bboxes(self, bboxes):
        bboxes[:, [1,3]] = self.image_size - bboxes[:, [3,1]]
        return bboxes

class VerticalFlip(Transform):
    def aug_img(self, image):
        return image.flip(2)
    
    def aug_images(self, images):
        return images.flip(3)
    
    def deaug_bboxes(self, bboxes):
        bboxes[:, [0,2]] = self.image_size - bboxes[:, [2,0]]
        return bboxes
    
class Rotate90(Transform):
    def aug_img(self, image):
        return torch.rot90(image, 1, (1, 2))

    def aug_images(self, images):
        return torch.rot90(images, 1, (2, 3))
    
    def deaug_bboxes(self, bboxes):
        rot_boxes = bboxes.copy()
        rot_boxes[:, [0,2]] = self.image_size - bboxes[:, [1,3]]
        rot_boxes[:, [1,3]] = bboxes[:, [2,0]]
        return rot_boxes

class Original(Transform):
    def aug_img(self, image):
        return image

    def aug_images(self, images):
        return images
    
    def deaug_bboxes(self, bboxes):
        return bboxes

class ToGray(Transform):
    tfms = torchvision.transforms.Compose([
        torchvision.transforms.ToPILImage(),
        torchvision.transforms.Grayscale(num_output_channels=3),
        torchvision.transforms.ToTensor()
    ])
    def aug_img(self, image):
        return self.tfms(image.cpu())

    def aug_images(self, images):
        images = [self.tfms(x.cpu()) for x in images]
        images = torch.stack(images).to(device)
        return images
    
    def deaug_bboxes(self, bboxes):
        return bboxes
    
class Hue(Transform):
    tfms = A.Compose(
        [
            A.HueSaturationValue(hue_shift_limit=0.2, sat_shift_limit= 0.2, val_shift_limit=0.2, p=1.0)
        ])
    def aug_img(self, image):
        image = image.permute(1,2,0).cpu().numpy()
        image = self.tfms(image=image)['image']
        return ToTensorV2()(image=image)['image']

    def aug_images(self, images):
        batch_size = len(images)
        for i in range(batch_size):
            images[i] = self.aug_img(images[i])
        return images
    
    def deaug_bboxes(self, bboxes):
        return bboxes
    
class Resize(Transform):
    def __init__(self, size):
        self.new_size = size
        self.tfms = torchvision.transforms.Compose([
            torchvision.transforms.ToPILImage(),
            torchvision.transforms.Resize((self.new_size,self.new_size)),
            torchvision.transforms.ToTensor()
        ])
    def aug_img(self, image):
        return self.tfms(image.cpu())

    def aug_images(self, images):
        images = [self.tfms(x.cpu()) for x in images]
        images = torch.stack(images).to(device)
        return images
    
    def deaug_bboxes(self, bboxes):
        bboxes = bboxes * self.image_size/self.new_size        
        return bboxes

class Compose(Transform):
    def __init__(self, transforms):
        self.transforms = transforms
        
    def aug_img(self, image):
        for transform in self.transforms:
            image = transform.aug_img(image)
        return image
    
    def aug_images(self, images):
        for transform in self.transforms:
            images = transform.aug_images(images)
        return images
    
    def prepare_boxes(self, boxes):
        bboxes = boxes.copy()
        bboxes[:,0] = np.min(boxes[:, [0,2]], axis=1)
        bboxes[:,2] = np.max(boxes[:, [0,2]], axis=1)
        bboxes[:,1] = np.min(boxes[:, [1,3]], axis=1)
        bboxes[:,3] = np.max(boxes[:, [1,3]], axis=1)
        return bboxes
    
    def deaug_bboxes(self, boxes):
        for transform in self.transforms[::-1]:
            boxes = transform.deaug_bboxes(boxes)
        return self.prepare_boxes(boxes)

t1 = Compose([Rotate90(), VerticalFlip(), HorizontalFlip()])
t2 = Compose([ToGray(), VerticalFlip(), HorizontalFlip()])
t3 = Compose([Hue(),Resize(512)])
t4 = Compose([Original()])
t5 = Compose([ToGray(), Resize(768)])
transforms = [t1, t2, t3, t4, t5]

from ensemble_boxes import *
def run_wbf(boxes, scores, image_size=511, iou_thr=0.5, skip_box_thr=0.7, weights=None):
    labels = [np.zeros(score.shape[0]) for score in scores]
    boxes = [box/(image_size) for box in boxes]
    boxes, scores, labels = weighted_boxes_fusion(boxes, scores, labels, weights=None, iou_thr=iou_thr, skip_box_thr=skip_box_thr)
    boxes = boxes*(image_size)
    return boxes, scores, labels

from utils.datasets import *
from utils.utils import *

def detect(img_path):    
    source = img_path
    weights = 'weights/best.pt'
    imgsz = 512
    conf_thres = 0.5
    iou_thres = 0.5

    imagenames =  os.listdir(source)

    device = torch.device('cuda') if torch.cuda.is_available() else torch.device('cpu')

    # Load model
    model = torch.load(weights, map_location=device)['model'].float()  # load to FP32
    model.to(device).eval()

    dataset = LoadImages(source, img_size=imgsz)

    half = False

    img = torch.zeros((1, 3, imgsz, imgsz), device=device)  # init img

    fig, ax = plt.subplots(5, 2, figsize=(30, 70))
    count = 0

    for path, img, im0s, vid_cap in dataset:

        img = torch.from_numpy(img).to(device)
        img = img.half() if half else img.float()  # uint8 to fp16/32
        img /= 255.0  # 0 - 255 to 0.0 - 1.0
        if img.ndimension() == 3:
            img = img.unsqueeze(0)
        im_w, im_h = img.shape[:2]
        enboxes = []
        enscores = []
        for transform in transforms:
            aug_img = transform.aug_img(img.squeeze(0))
            aug_img = aug_img.unsqueeze(0)
            
            pred = model(aug_img.to(device), augment=False)[0]
            pred = non_max_suppression(pred, conf_thres, iou_thres)

            bboxes = []
            score = []
            # Process detections
            for i, det in enumerate(pred):  # detections per image
                p, s, im0 = path, '', im0s
                gn = torch.tensor(im0.shape)[[1, 0, 1, 0]]  #  normalization gain whwh
                if det is not None and len(det):
                    det[:, :4] = scale_coords(img.shape[2:], det[:, :4], im0.shape).round()
                    for c in det[:, -1].unique():
                        n = (det[:, -1] == c).sum()  # detections per class

                    for *xyxy, conf, cls in det:
                        if True:  # Write to file
                            xywh = torch.tensor(xyxy).view(-1).numpy()  # normalized xywh
                            bboxes.append(xywh)
                            score.append(conf)
            boxes , scores = np.array(bboxes), np.array(score)        
            boxes = transform.deaug_bboxes(boxes.copy())

            enboxes.append(boxes)
            enscores.append(scores)
        boxes, scores, labels = run_wbf(enboxes, enscores, image_size = im_w, iou_thr=0.45, skip_box_thr=0.5)
        boxes = boxes.astype(np.int32).clip(min=0, max=512)

        boxes[:, 2] = boxes[:, 2] - boxes[:, 0]
        boxes[:, 3] = boxes[:, 3] - boxes[:, 1]

        boxes = boxes[scores >= 0.45].astype(np.int32)
        scores = scores[scores >=float(0.45)]
        # plot the images
        for box, score in zip(boxes,scores):
            cv2.rectangle(im0,
                          (box[0], box[1]),
                          (box[2]+box[0], box[3]+box[1]),
                          (220, 0, 0), 2)
            cv2.putText(im0, '%.2f'%(score), (box[0], box[1]), cv2.FONT_HERSHEY_SIMPLEX ,  
               0.5, (255,255,255), 2, cv2.LINE_AA)
        ax[count%5][count//5].imshow(im0)
        count+=1