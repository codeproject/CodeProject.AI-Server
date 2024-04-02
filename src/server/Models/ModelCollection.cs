// -------------------------------------------------------------------------------------------------
// This file is a starting point for handling the Models in a Model Zoo. The Model Zoo is a
// collection of models that can be used by the AI Server. The classes in this file are used to
// describe the models in the Model Zoo. This just a starting point and included in the project to 
// show how the models could be described. The actual implementation will depend on the requirements
// of the AI Server and the Model Zoo.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CodeProject.AI.Server.Models
{
    /// <summary>
    /// <para>
    /// The collection of model hierarchies. At the top we have <see cref="ModelGroup"/> , which
    /// defines a group of models that satisfy a specific task type, for a given set of
    /// classification classes, trained for a specific architecture and saved as a specific format.
    /// Under that we have a set of <see cref="ModelPackage"/>s that provide different "sized"
    /// models. "Size" could (and often does) mean physical size, but could also represent the number
    /// of layers or parameters or overall complexity of a given model. A <see cref="ModelPackage"/>
    /// contains a set of <see cref="ModelSizeVariants"/>s which represent a single size of a model. This
    /// is required because a single size of a model may be represented by multiple files: one for
    /// CPU, one for GPU, and maybe a set of segmented files. Each <see cref="ModelSizeVariants"/>
    /// contains the actual <see cref="ModelFile"/> objects, which refer to physical files.
    /// </para>
    /// <para>
    /// This collection object will be deserialised from the collection of model JSON files that
    /// represent the collection of <see cref="ModelPackage"/>s available for download. Implicit
    /// here is that we aren't supporting downloading of individual files, only individual packages.
    /// Having said that, a package may include only a single model file. However, that package will
    /// have a full <see cref="ModelPackage"/> specification.
    /// </para>
    /// <para>
    /// Since we're deserialising from JSON files, we will have objects contain sub-objects rather
    /// than having an object contain a reference to its parent (though we could add this as a post-
    /// load step).
    /// </para>
    /// </summary>
    public class ModelCollection : List<ModelGroup>
    {
    }
    
    /// <summary>
    /// Represents a group of related Model Packages for a particular Task and Architecture. See
    /// also <see cref="ModelPackage"/>
    /// </summary>
    public class ModelGroup
    {
        /// <summary>
        /// Gets or sets the Id of the model.
        /// </summary>
        [JsonIgnore]
        public string ModelGroupId => Category + "-" + Task;

        /// <summary>
        /// Gets or sets the human readable name of the model.
        /// </summary>
        [JsonIgnore]
        public string Name => Category + " / " + Task;

        /// <summary>
        /// Gets or sets the description of the model.
        /// </summary>
        [JsonIgnore]
        public string Description => "Models for " + Category + " that perform " + Task;

        /// <summary>
        /// The category this model belongs to. This could be things like 'Vision', 'Audition'
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the tasks the model can perform. Eg: 'ObjectDetection', 'Generative AI', etc.
        /// </summary>
        public string? Task { get; set; }

        /// <summary>
        /// Gets or sets the Model Packages in the Model Group. See also <see cref="ModelPackage"/>
        /// </summary>
        public ModelPackage[] Packages { get; set; } = new ModelPackage[0];
    }

    /// <summary>
    /// The attributes of a model package (ie a set of models that share the same attributes) or a
    /// model file itself that can be used by a Module.
    /// </summary>
    /// <remarks>
    /// This is used in the modulesettings.json file to describe the attributes of a model that a
    /// module can use.
    /// </remarks>
    public class ModelPackageAttributes
    {
        /// <summary>
        /// Gets or sets the architecture of the model. Eg: 'YoloV5.6.2', 'ResNet50', etc.
        /// </summary>
        public string? Architecture { get; set; }

        /// <summary>
        /// Gets or sets the tasks the model can perform. Eg: 'ObjectDetection', 'Generative', etc.
        /// </summary>
        /// <remarks>POST-LOAD: This needs to come from the parent ModelGroup and be set in a post-
        /// load step.</remarks>
        public string? Task { get; set; }

        /// <summary>
        /// Gets or sets the format the model is stored in. Eg: 'ONNX', 'TensorFlow', 'PyTorch', etc.
        /// </summary>
        public string? Format { get; set; }
    }

    /// <summary>
    /// Represents a variant of a model. Variant is a different version of the same model and could
    /// be different in model architecture, size, format, etc.
    /// </summary>
    public class ModelPackage: ModelPackageAttributes
    {
        /// <summary>
        /// Gets or sets the name of this package. It should be systematised to be something like
        /// "ObjectDetection for Animals on YOLOv5, PyTorch"
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets a description this package. Again, generally needs to be systematised but
        /// may include information on where the dataset came from, specifics on the classes
        /// supported, and maybe suitable applications for this set of models.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the classes this model package handles. eg "animals", "COCO", "vehicles"
        /// </summary>
        public string? Classes { get; set; }

        /// <summary>
        /// Gets or sets the zip file name of the zip model package.
        /// eg objectdetection-animals-yolov5-pt-nsmlh-1.0.zip
        /// </summary>
        public string? Filename  { get; set; }

        /// <summary>
        /// Gets or sets the download URL of the model package file.
        /// </summary>
        /// <remarks>POST-LOAD: This needs to be set in a post-load step.</remarks>
        public string? PackageDownloadUrl { get; set; } // => StorageUrl + PackageFilename;

        /// <summary>
        /// Gets or sets the size of the model package file in bytes.
        /// </summary>
        public long PackageSize { get; set; }

        /// <summary>
        /// Gets or sets the version of the Model Package.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the entity who created the model.
        /// </summary>
        public string? Creator { get; set; }

        /// <summary>
        /// Gets or sets the license of the model.
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the list of files included in this package. Every model file must serve the
        /// same task, for the same set of classes, the same architecture and in the same format. 
        /// The difference in files will be size (small, med, large), or device specific variants
        /// (eg CPU and GPU version). See also <see cref="ModelSets"/>
        /// </summary>
        public ModelSizeVariants[] ModelSets { get; set; } = new ModelSizeVariants[0];
    }

    /// <summary>
	/// All files needed to provide a module with files for a single model size. Generally only 
    /// ModelFile is used, but some systems such as Coral TPUs have a TPU specific file and may also
    /// have TPU segment files as an option.
    /// </summary>
	public class ModelSizeVariants
	{
        /// <summary>
        /// Gets or sets the "size" of the model. Size is relative, so this is generally "small",
        /// "medium", "large" etc based on the conventions usually in force for this model family.
        /// </summary>
		public string? SizeName { get; set; }
		
        /// <summary>
        /// The name of the file containing the class labels (if appropriate)
        /// </summary>
        public string? LabelFile { get; set; }

        /// <summary>
        /// The default file (objectdetection-animals-yolov5-tflite-m.tflite)
        /// </summary>
		public ModelFile? ModelFile { get; set; }

        /// <summary>
        /// If there's a specific file for hardware (objectdetection-animals-yolov5-tflite-m-edgetpu.pt)
        /// </summary>
		public ModelFile? DeviceSpecificFile { get; set; }

        /// <summary>
        ///  List of partitioned/segment files, eg [objectdetection-cars-yolov5-tf-m-1_of_2.tf, ...]
        /// </summary>
		public ModelFile[]? PartialFiles { get; set; }
	}   

    /// <summary>
    /// Represents information about a file in a set of files representing a "Model of a given Size"
    /// inside a model package.
    /// </summary>
    /// <remarks>
    /// Currently just filename and size, but could include other things such as date created etc
    /// </remarks>
    public class ModelFile
    {
        /// <summary>
        /// Gets or sets the "size" of the model. Size is relative, so this is generally "small",
        /// "medium", "large" etc based on the conventions usually in force for this model family.
        /// </summary>
        /// <remarks>POST-LOAD: This should be checked against the parent ModelSizeSet to ensure
        /// <see cref="SizeName"/> Size matches <see cref="ModelSizeVariants.SizeName"/>.</remarks>
  		public string? SizeName { get; set; }

        /// <summary>
        /// Gets or sets the size of the file in bytes
        /// </summary>
        public string? FileSize { get; set; }

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
  		public string? Filename { get; set; }

        /// <summary>
        /// Gets or sets the specifier string of the file, which distinguishes it based on specific
        /// use. For example a segmented TPU file may have "1_of_3-edgetpu" as the specifier. This
        /// value is only used for auto-generating (or validating) filenames.
        /// </summary>
		public string? FileSpecifier { get; set;  } 
    }

    static class ModelPackageExtensions
    {
        /// <summary>
        /// To automatically generate a filename for a given package.
        /// </summary>
        /// <param name="package">This package</param>
        /// <returns></returns>
        public static string GetFilename(this ModelPackage package)
        {
    		string sizeList         = string.Join("", package.ModelSets.Select(m => m.SizeName?.ToLower()[0]));
            string sizeSpecifier    = string.IsNullOrWhiteSpace(sizeList)? string.Empty : $"-{sizeList}";
            string classesSpecifier = string.IsNullOrWhiteSpace(package.Classes) 
                                    ? string.Empty : $"-{package.Classes}";
            // eg objectdetection-animals-yolov5
            string basename = $"{sizeSpecifier}{classesSpecifier}-{package.Architecture?.Replace("-","-")??""}";

            // eg objectdetection-animals-yolov5-pt-sml-1.0.zip
            return $"{basename}-{package.FormatSpecifier()}-{sizeList}-{package.Version}.zip";
        }

        /// <summary>
        /// To automatically generate a filename for a given file. Note that the files in the 
        /// package themselves consist of multiple files for a single model (eg Paddle) which could
        /// be stored in a folder. For these files, string.Empty is returned since there is no one
        /// answer.
        /// </summary>
        /// <param name="package">This package</param>
        /// <param name="modelFile">The model file whose name we wish to generate</param>
        /// <returns></returns>
        public static string GetFilename(this ModelPackage package, ModelFile modelFile)
        {
            string classesSpecifier = string.IsNullOrWhiteSpace(package.Classes)         // eg "animals" or ""
                                    ? string.Empty : $"-{package.Classes}";
            string sizeSpecifier    = modelFile.SizeName?[0] is null? string.Empty: "-" + modelFile.SizeName[0]; // "-m" or ""
            string fileSpecifier    = string.IsNullOrWhiteSpace(modelFile.FileSpecifier) // eg _1_of_3 or ""
                                    ? string.Empty : "-" + modelFile.FileSpecifier;
            string nameSuffix       = package.NameSuffix();                              // eg _edgetpu or ""
            string extension        = package.FormatExtension();                         // eg .tflite

            // eg objectdetection-animals-yolov5
            //    ocr-pp_ocrv3
            string basename = $"{package.Task}{classesSpecifier}-{package.Architecture?.Replace("-","-")??""}";

            // eg objectdetection-cars-yolov5-m-1_of_2_edgutpu.tflite or
            //    objectdetection-cars-yolov5-m.pt
            return $"{basename}{sizeSpecifier}{fileSpecifier}{nameSuffix}.{extension}";
        }

        /// <summary>
        /// Gets the suffix to append to a filename for a given format
        /// </summary>
        /// <param name="package">This package</param>
        /// <returns>A string</returns>
        public static string NameSuffix(this ModelPackage package)
        {
            return package.Format?.ToLower() switch
            {
                "tensorflow-edgetpu"     => "_edgetpu", // TensorFlow Edge TPU
                _                        => string.Empty
            };
        }

        /// <summary>
        /// Gets the format specifier (the file extension without the ".") for a given file format.
        /// Some models, such as PaddlePaddle, contain multiple files of differing extensions so
        /// this specifier is not always a file extension, but is used in model package zip file names
        /// </summary>
        /// <param name="package"></param>
        /// <returns>A string</returns>
        /// <remarks>
        ///  - TensorFlow saved_model saved in _saved_model/ folder
        ///  - PaddlePaddle models saved in _paddle_model/ folder
        /// </remarks>
        public static string FormatSpecifier(this ModelPackage package)
        {
            return package.Format?.ToLower() switch
            {
                "coreml"                 => "coreml",         // CoreML
                "pytorch"                => "pt",             // PyTorch
                "onnxruntime"            => "onnx",           // ONNX Runtime
                "onnxruntime-opencv-dnn" => "onnx",           // ONNX OpenCV DNN
                "openvino"               => "openvino",       // OpenVINO
                "paddlepaddle"           => "paddle",         // PaddlePaddle      
                "rknn"                   => "rknn",           // Rockchip Neural network
                "tensorrt"               => "tensorrt",       // TensorRT
                "tensorflow-edgetpu"     => "tflite",         // TensorFlow Edge TPU, filename has _edgetpu suffix
                "tensorflow-graphdef"    => "tfgraph",        // TensorFlow GraphDef
                "tensorflow-lite"        => "tflite",         // TensorFlow Lite
                "torchscript"            => "torchscript",    // TorchScript
                _                        => string.Empty
            };
        }

        /// <summary>
        /// Gets the extension (with the ".") for a given file format. Returns empty if there is no
        /// single extension for a given format. eg PaddlePaddle models contain multiple files of
        /// differing extensions.
        /// </summary>
        /// <param name="package"></param>
        /// <returns>A string</returns>
        /// <remarks>
        ///  - TensorFlow saved_model saved in _saved_model/ folder
        ///  - PaddlePaddle models saved in _paddle_model/ folder
        /// </remarks>
        public static string FormatExtension(this ModelPackage package)
        {
            return package.Format?.ToLower() switch
            {
                "coreml"                 => "mlmodel",        // CoreML
                "pytorch"                => "pt",             // PyTorch
                "onnxruntime"            => "onnx",           // ONNX Runtime
                "onnxruntime-opencv-dnn" => "onnx",           // ONNX OpenCV DNN
                "openvino"               => "xml",            // OpenVINO
                "paddlepaddle"           => "pdmodel",        // PaddlePaddle      
                "rknn"                   => "rknn",           // Rockchip Neural network
                "tensorrt"               => "engine",         // TensorRT
                "tensorflow-edgetpu"     => "tflite",         // TensorFlow Edge TPU, filename has _edgetpu suffix
                "tensorflow-graphdef"    => "pb",             // TensorFlow GraphDef
                "tensorflow-lite"        => "tflite",         // TensorFlow Lite
                "torchscript"            => "torchscript",    // TorchScript
                _                        => string.Empty
            };
        }
    }
}
