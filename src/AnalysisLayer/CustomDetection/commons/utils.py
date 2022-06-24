import torch
import torch.nn as nn
import torch.nn.functional as F


def load_model(model, path):
    checkpoint = torch.load(path, map_location=lambda storage, loc: storage)

    try:
        model.load_state_dict(checkpoint)

    except:
        copy = dict()
        for x, y in zip(model.state_dict(), checkpoint):
            new_name = y[y.index(x) :]
            copy[new_name] = checkpoint[y]


def l2_norm(input, axis=1):
    norm = torch.norm(input, 2, axis, True)
    output = torch.div(input, norm)
    return output


def compute_distance(embeddings, embeddings2):

    diff = embeddings.unsqueeze(-1) - embeddings2.transpose(1, 0).unsqueeze(0)
    distance = torch.sum(torch.pow(diff, 2), dim=1)

    return distance


class _GlobalPoolNd(nn.Module):
    def __init__(self, flatten=True):
        """

        :param flatten:
        """
        super(_GlobalPoolNd, self).__init__()
        self.flatten = flatten

    def pool(self, input):
        """

        :param input:
        :return:
        """
        raise NotImplementedError()

    def forward(self, input):
        """

        :param input:
        :return:
        """
        input = self.pool(input)
        size_0 = input.size(1)
        return input.view(-1, size_0) if self.flatten else input


class GlobalAvgPool2d(_GlobalPoolNd):
    def __init__(self, flatten=True):
        """

        :param flatten:
        """
        super(GlobalAvgPool2d, self).__init__(flatten)

    def pool(self, input):
        return F.adaptive_avg_pool2d(input, 1)


class Flatten(nn.Module):
    def forward(self, input):
        return input.view(input.size(0), -1)


class UpSampleInterpolate(nn.Module):
    def __init__(self, scale_factor):
        super(UpSampleInterpolate, self).__init__()

        self.scale_factor = scale_factor

    def forward(self, x):

        return F.interpolate(x, scale_factor=self.scale_factor, mode="nearest")
