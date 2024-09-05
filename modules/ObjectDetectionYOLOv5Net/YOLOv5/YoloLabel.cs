namespace Yolov5Net.Scorer
{
    /// <summary>
    /// Label of detected object.
    /// </summary>
    public struct YoloLabel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
