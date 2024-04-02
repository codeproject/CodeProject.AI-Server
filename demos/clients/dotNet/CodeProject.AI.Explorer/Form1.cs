using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using CodeProject.AI.API;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;

using SkiaSharp.Views.Desktop;

namespace CodeProject.AI.Demo.Explorer
{
    // NOTE: not using .ConfigureAwait(false) calls here since we need the continuation code to run
    // on the same context as the caller code in order to ensure we access UI elements on the UI
    // thread.   
    public partial class Form1 : Form
    {
        const int _pingFrequency = 2;    // seconds
        const int _apiServerPort = 32168; // be default

        private readonly ServerClient _serverClient           = new(_apiServerPort);
        private bool                  _serverLive             = false;    
        private string                _imageFileName          = string.Empty;
        private string                _faceImageFileName1     = string.Empty;
        private string                _faceImageFileName2     = string.Empty;
        private string                _recognizeImageFileName = string.Empty;
        private string                _benchmarkFileName      = string.Empty;
        private readonly List<string> _registerFileNames      = new();

        private readonly Font         _textFont               = new("Arial", 7);
        private readonly SolidBrush   _textBrush              = new(Color.White);
        private readonly Pen          _boundingBoxPen         = new(Color.Yellow, 2);

        public Form1()
        {
            InitializeComponent();
            textApiPort.Text = _apiServerPort.ToString();

            timer1.Interval = _pingFrequency * 1000;
            timer1.Enabled  = true;
            timer1.Tick    += new EventHandler(Ping!);
        }

        private async void Ping(object sender, EventArgs e)
        {
            var response = await _serverClient.Ping();
            if (_serverLive != response.Success)
            {
                _serverLive = response.Success;
                if (response.Success)
                    SetStatus("Connection to AI Server established", Color.Green);
                else
                    ShowError("Unable to connect to AI server");
            }
        }

        private void ImageSelect_Click(object sender, EventArgs e)
        {
            DetectFaceBtn.Enabled = DetectSceneBtn.Enabled = DetectObjectsBtn.Enabled = false;

            var fileDialogResult = openFileDialog.ShowDialog();
            if (fileDialogResult == DialogResult.OK)
            {
                _imageFileName     = openFileDialog.FileName;
                ImageFileName.Text = _imageFileName;

                ClearResults();
            }

            bool enable = !string.IsNullOrWhiteSpace(_imageFileName);
            DetectFaceBtn.Enabled = DetectSceneBtn.Enabled = DetectObjectsBtn.Enabled = enable;
        }

        private void ImageFileName_TextChanged(object sender, EventArgs e)
        {
            _imageFileName = ImageFileName.Text;

            bool enable = !string.IsNullOrWhiteSpace(_imageFileName);
            DetectFaceBtn.Enabled = DetectSceneBtn.Enabled = DetectObjectsBtn.Enabled = enable;
        }

        private void FaceImage1Select_Click(object sender, EventArgs e)
        {
            CompareFacesBtn.Enabled = false;

            var fileDialogResult = openFileDialog.ShowDialog();
            if (fileDialogResult == DialogResult.OK)
            {
                _faceImageFileName1     = openFileDialog.FileName;
                FaceImageFileName1.Text = _faceImageFileName1;

                ClearResults();
            }

            CompareFacesBtn.Enabled = !string.IsNullOrWhiteSpace(_faceImageFileName1) &&
                                      !string.IsNullOrWhiteSpace(_faceImageFileName2);
        }

        private void FaceImageFileName1_TextChanged(object sender, EventArgs e)
        {
            _faceImageFileName1 = FaceImageFileName1.Text;
            CompareFacesBtn.Enabled = !string.IsNullOrWhiteSpace(_faceImageFileName1) &&
                                      !string.IsNullOrWhiteSpace(_faceImageFileName2);
        }

        private void FaceImage2Select_Click(object sender, EventArgs e)
        {
            CompareFacesBtn.Enabled = false;

            var fileDialogResult = openFileDialog.ShowDialog();
            if (fileDialogResult == DialogResult.OK)
            {
                _faceImageFileName2 = openFileDialog.FileName;
                FaceImageFileName2.Text = _faceImageFileName2;

                ClearResults();
            }

            CompareFacesBtn.Enabled = !string.IsNullOrWhiteSpace(_faceImageFileName1) &&
                                      !string.IsNullOrWhiteSpace(_faceImageFileName2);
        }

        private void FaceImageFileName2_TextChanged(object sender, EventArgs e)
        {
            _faceImageFileName2 = FaceImageFileName2.Text;
            CompareFacesBtn.Enabled = !string.IsNullOrWhiteSpace(_faceImageFileName1) &&
                                      !string.IsNullOrWhiteSpace(_faceImageFileName2);
        }
        private void RecognizeImageSelect_Click(object sender, EventArgs e)
        {
            RecognizeFaceBtn.Enabled = false;

            var fileDialogResult = openFileDialog.ShowDialog();
            if (fileDialogResult == DialogResult.OK)
            {
                _recognizeImageFileName     = openFileDialog.FileName;
                RecognizeImageFileName.Text = _recognizeImageFileName;

                ClearResults();
            }

            RecognizeFaceBtn.Enabled = !string.IsNullOrWhiteSpace(_recognizeImageFileName);
        }

        private void RecognizeImageFileName_TextChanged(object sender, EventArgs e)
        {
            _recognizeImageFileName = RecognizeImageFileName.Text;
            RecognizeFaceBtn.Enabled = !string.IsNullOrWhiteSpace(_recognizeImageFileName);
        }

        private async void DetectFaceBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Detecting faces.");

            if (string.IsNullOrWhiteSpace(_imageFileName))
            {
                ShowError("Image 1 must be selected.");
                return;
            }

            var result = await _serverClient.DetectFaces(_imageFileName);
            if (result is DetectFacesResponse detectedFaces)
            {
                Image? image = GetImage(_imageFileName);
                if (image == null)
                {
                    ShowError($"Unable to load image {_imageFileName}.");
                    return;
                }

                Graphics canvas = Graphics.FromImage(image);

                List<string> lines = new();

                if (detectedFaces.Predictions is not null)
                {
                    foreach (var (face, index) in detectedFaces.Predictions
                        .Select((face, index) => (face, index)))
                    {
                        lines.Add($"{index}: Confidence: {Math.Round(face.Confidence*100.0, 2)}");

                        var rect = Rectangle.FromLTRB(face.X_min, face.Y_min, face.X_max, face.Y_max);
                        canvas.DrawRectangle(_boundingBoxPen, rect);
                        canvas.DrawString(index.ToString(), _textFont, _textBrush, face.X_min, face.Y_min);
                    }

                    detectionResult.Lines = lines.ToArray();
                }

                pictureBox1.Image = image;
                SetStatus("Face Detection complete");
            }
            else
            {
                ProcessError(result);
            }
        }

        private async void CompareFacesBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Comparing faces");

            if (string.IsNullOrWhiteSpace(_faceImageFileName1))
            {
                ShowError("Image 1 must be selected.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_faceImageFileName2))
            {
                ShowError("Image 2 must be selected.");
                return;
            }

            var result = await _serverClient.MatchFaces(_faceImageFileName1, _faceImageFileName2);
            if (result is MatchFacesResponse matchedFaces)
            {
                detectionResult.Text = $"Similarity: {Math.Round(matchedFaces.Similarity, 4)}";
                SetStatus("Face Comparison complete");
            }
            else
            {
                ProcessError(result);
            }
        }

        private async void DetectSceneBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Detecting scene");

            if (string.IsNullOrWhiteSpace(_imageFileName))
            {
                ShowError("Image 1 must be selected.");
                return;
            }

            var result = await _serverClient.DetectScene(_imageFileName);
            if (result is DetectSceneResponse detectedScene)
            {
                var image = GetImage(_imageFileName);
                pictureBox1.Image = image;

                detectionResult.Text = $"Confidence: {Math.Round(detectedScene.Confidence*100.0, 2)} Label: {detectedScene.Label}";
                SetStatus("Scene Detection complete");
            }
            else
            {
                ProcessError(result);
            }
        }

        private async void DetectObjectsBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Detecting objects");

            if (string.IsNullOrWhiteSpace(_imageFileName))
            {
                ShowError("Image 1 must be selected.");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            ServerResponse result = await _serverClient.DetectObjects(_imageFileName);
            stopwatch.Stop();

            if (result is DetectObjectsResponse detectedObjects)
            {
                List<string> lines = new();
                try
                {
                    lines.Add(stopwatch.Elapsed.ToString());
                    if (detectedObjects.Predictions is not null)
                        foreach (var (detectedObj, index) in detectedObjects.Predictions
                            .Select((detected, index) => (detected, index)))
                        {
                            lines.Add($"{index}: Conf: {Math.Round(detectedObj.Confidence*100.0, 2)} {detectedObj.Label}");

                            var rect = Rectangle.FromLTRB(detectedObj.X_min, detectedObj.Y_min, 
                                                          detectedObj.X_max, detectedObj.Y_max);
                        }

                    detectionResult.Lines = lines.ToArray();

                    Image? image = GetImage(_imageFileName);
                    if (image == null)
                    {
                        ShowError($"Unable to load image {_imageFileName}.");
                        return;
                    }

                    Graphics canvas  = Graphics.FromImage(image);

                    if (detectedObjects.Predictions is not null)
                    {
                        foreach (var (detectedObj, index) in detectedObjects.Predictions
                            .Select((detected, index) => (detected, index)))
                        {
                            var rect = Rectangle.FromLTRB(detectedObj.X_min, detectedObj.Y_min,
                                                          detectedObj.X_max, detectedObj.Y_max);
                            canvas.DrawRectangle(_boundingBoxPen, rect);
                            canvas.DrawString($"{index}:{detectedObj.Label}", _textFont, _textBrush, 
                                              detectedObj.X_min, detectedObj.Y_min);
                        }
                    }

                    SetStatus("Object Detection complete");
                    pictureBox1.Image = image;
                }
                catch (Exception ex)
                {
                    ProcessError(new ServerErrorResponse (ex.Message));
                    detectionResult.Lines = lines.ToArray();
                }
            }
            else
            {
                ProcessError(result);
            }
        }

        private void OnUserIdTextboxChanged(object sender, EventArgs e)
        {
            DeleteFaceBtn.Enabled = !string.IsNullOrWhiteSpace(UserIdTextbox.Text);
        }

        private async void RegisterFaceBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Registering faces");

            if (_registerFileNames == null || _registerFileNames.Count == 0)
            {
                ShowError("You must supply images of this face to register.");
                return;
            }

            var result = await _serverClient.RegisterFace(UserIdTextbox.Text, _registerFileNames);
            if (result is RegisterFaceResponse registeredFace)
                SetStatus("Registration complete");
            else
                ProcessError(result);
        }

        private async void RecognizeFaceBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Searching for known faces");

            string? filename = _recognizeImageFileName;
            if (string.IsNullOrWhiteSpace(filename))
            {
                ShowError("No face files have been registered for detection.");
                return;
            }

            if (!float.TryParse(MinConfidence.Text, out float minConfidence))
                minConfidence = 0.4f;

            var result = await _serverClient.RecognizeFace(filename, minConfidence);
            if (result is RecognizeFacesResponse recognizeFace)
            {
                try
                {
                    Image? image = GetImage(filename);
                    if (image == null)
                    {
                        Status.Text = $"Unable to load image {filename}.";
                        return;
                    }

                    Graphics canvas = Graphics.FromImage(image);

                    List<string> lines = new();
                    if (recognizeFace.Predictions is not null)
                    {
                        var predictionMap = recognizeFace.Predictions
                                                         .Select((prediction, Index) => (prediction, Index));
                        foreach (var (prediction, index) in predictionMap)
                        {
                            lines.Add($"{index}: Conf: {Math.Round(prediction.Confidence*100.0, 2)} {prediction.Userid}");
                            var rect = Rectangle.FromLTRB(prediction.X_min, prediction.Y_min, 
                                                          prediction.X_max, prediction.Y_max);
                            canvas.DrawRectangle(_boundingBoxPen, rect);
                            canvas.DrawString($"{index}:{prediction.Userid}", _textFont, _textBrush, 
                                              prediction.X_min, prediction.Y_min);
                        }

                        detectionResult.Lines = lines.ToArray();
                    }

                    SetStatus("Face recognition complete");
                    pictureBox1.Image = image;
                }
                catch (Exception ex)
                {
                    ProcessError(new ServerErrorResponse($"{filename} caused: {ex.Message}"));
                }
            }
            else
                ProcessError(result);
        }

        private async void ListFacesBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Listing known faces");

            var result = await _serverClient.ListRegisteredFaces();
            if (result is ListRegisteredFacesResponse registeredFaces)
            {
                if (result?.Success ?? false)
                {
                    List<string> lines = new();
                    if (registeredFaces.Faces != null)
                    {
                        var faceMap = registeredFaces.Faces.Select((face, Index) => (face, Index));
                        foreach (var (face, index) in faceMap)
                        {
                            lines.Add($"{index}: {face}");
                        }

                        detectionResult.Lines = lines.ToArray();
                        SetStatus("Face Listing complete");
                    }
                }
            }
            else
                ProcessError(result);
        }

        private void SelectRegisterImages_Click(object sender, EventArgs e)
        {
            RegisterFaceImagesBtn.Enabled = false;

            var fileDialogResult = selectFilesDlg.ShowDialog();
            if (fileDialogResult == DialogResult.OK)
            {
                _registerFileNames.Clear();
                foreach (var fileNames in selectFilesDlg.FileNames)
                    _registerFileNames.Add(fileNames);

                ClearResults();
            }

            RegisterFaceImagesBtn.Enabled = _registerFileNames.Count > 0;
        }

        private async void DeleteFaceBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Deleting registered face");

            var result = await _serverClient.DeleteRegisteredFace(UserIdTextbox.Text);
            if (result?.Success ?? false)
                SetStatus("Completed Face deletion");
            else
                ProcessError(result);
        }

        /* --- Benchmarking --- */
        private void benchmarkImageSelectButton_Click(object sender, EventArgs e)
        {
            var fileDialogResult = openFileDialog.ShowDialog();
            if (fileDialogResult == DialogResult.OK)
            {
                _benchmarkFileName = openFileDialog.FileName;
                BenchmarkFileName.Text = _benchmarkFileName;

                ClearResults();
            }
        }

        private void BenchFileName_TextChanged(object sender, EventArgs e)
        {
            _benchmarkFileName = BenchmarkFileName.Text;

            bool enable = !string.IsNullOrWhiteSpace(_benchmarkFileName);
            BenchmarkRunStdBtn.Enabled = BenchmarkRunCustomBtn.Enabled = enable;
        }

        private void BenchmarkRunCustomBtn_Click(object sender, EventArgs e)
        {
            RunBenchmark(true);
        }

        private void BenchmarkRunStdBtn_Click(object sender, EventArgs e)
        {
            RunBenchmark(false);
        }

        private async void RunBenchmark(bool useCustom)
        {
            ClearResults();
            SetStatus("Benchmark running");

            if (string.IsNullOrWhiteSpace(_benchmarkFileName))
            {
                ShowError("Image must be selected.");
                return;
            }
            var nIterations = 50;
            var taskList = new List<Task<ServerResponse>>();
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < nIterations; i++){
                var task = useCustom 
                         ?_serverClient.CustomDetectObjects(_benchmarkFileName, "ipcam-general")
                         : _serverClient.DetectObjects(_benchmarkFileName);
                taskList.Add(task);
            }
            await Task.WhenAll(taskList);
            sw.Stop();

            BenchmarkResults.Text = $"Benchmark: {Math.Round(nIterations / (sw.ElapsedMilliseconds/ 1000.0), 2)} FPS";
            SetStatus("Benchmark complete");
        }

        private void ClearResults()
        {
            pictureBox1.Image = null;
            detectionResult.Clear();
        }

        private void SetStatus(string text, Color? color = null)
        {
            // We need to allow this to be updated from another thread.
            Invoke((MethodInvoker)delegate ()
            {
                Status.Text      = text;
                Status.ForeColor = color ?? Color.Black;
            });
        }

        private void ShowError(string text)
        {
            SetStatus(text, Color.Red);
        }

        private void ProcessError(ServerResponse? result)
        {
            pictureBox1.Image    = null;
            detectionResult.Text = string.Empty;

            if (result is ServerErrorResponse response)
                ShowError($"Error: {response.Code} - {response.Error ?? "No Error Message"}");
            else if (result is null)
                ShowError("Null result");
            else
                ShowError("Invalid response");
        }

        /// <summary>
        /// Loads a Bitmap from a file.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <returns>The Bitmap, or null.</returns>
        /// <remarks>SkiSharp handles more image formats than System.Drawing.</remarks>
        private Image? GetImage(string filename)
        {
            var skiaImage = ImageUtils.GetImage(filename);
            if (skiaImage is null)
                return null;

            return skiaImage.ToBitmap();
        }

        private void OnApiPortChanged(object sender, EventArgs e)
        {
            if (int.TryParse(textApiPort.Text, out int port))
               _serverClient.Port = port;
        }
    }
}
