using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using CodeProject.SenseAI.API.Common;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace CodeProject.SenseAI.Demo.Playground
{
    public partial class Form1 : Form
    {
        const int _pingFrequency = 2;    // seconds
        const int _apiServerPort = 5000; // be default

        private ApiClient    _AIService              = new ApiClient(_apiServerPort);
        private bool         _serverLive             = false;    
        private string       _imageFileName          = string.Empty;
        private string       _faceImageFileName1     = string.Empty;
        private string       _faceImageFileName2     = string.Empty;
        private string       _recognizeImageFileName = string.Empty;
        private List<string> _registerFileNames      = new();

        public Form1()
        {
            InitializeComponent();
            textApiPort.Text = _apiServerPort.ToString();

            timer1.Interval = _pingFrequency * 1000;
            timer1.Enabled  = true;
            timer1.Tick += new EventHandler(Ping!);
        }
        private async void Ping(object sender, EventArgs e)
        {
            var response = await _AIService.Ping();
            if (_serverLive != response.success)
            {
                _serverLive = response.success;
                if (response.success)
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

            var result = await _AIService.DetectFaces(_imageFileName);
            if (result is DetectFacesResponse detectedFaces)
            {
                Image? image = GetImage(_imageFileName);
                if (image == null)
                {
                    ShowError($"Unable to load image {_imageFileName}.");
                    return;
                }

                Graphics canvas  = Graphics.FromImage(image);
                Pen pen          = new Pen(Color.Yellow, 2);
                SolidBrush brush = new(Color.White);
                Font drawFont    = new Font("Arial", 10);

                List<string> lines = new();

                if (detectedFaces.predictions is not null)
                {
                    foreach (var (face, index) in detectedFaces.predictions
                        .Select((face, index) => (face, index)))
                    {
                        lines.Add($"{index}: Confidence: {Math.Round(face.confidence, 3)}");

                        var rect = Rectangle.FromLTRB(face.x_min, face.y_min, face.x_max, face.y_max);
                        canvas.DrawRectangle(pen, rect);
                        canvas.DrawString(index.ToString(), drawFont, brush, face.x_min, face.y_min);
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

            var result = await _AIService.MatchFaces(_faceImageFileName1, _faceImageFileName2);
            if (result is MatchFacesResponse matchedFaces)
            {
                detectionResult.Text = $"Similarity: {Math.Round(matchedFaces.similarity, 4)}";
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

            var result = await _AIService.DetectScene(_imageFileName);
            if (result is DetectSceneResponse detectedScene)
            {
                var image = GetImage(_imageFileName);
                pictureBox1.Image = image;

                detectionResult.Text = $"Confidence: {Math.Round(detectedScene.confidence, 3)} Label: {detectedScene.label}";
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
            var result          = await _AIService.DetectObjects(_imageFileName);
            stopwatch.Stop();

            if (result is DetectObjectsResponse detectedObjects)
            {
                List<string> lines = new();
                try
                {
                    lines.Add(stopwatch.Elapsed.ToString());
                    if (detectedObjects.predictions is not null)
                        foreach (var (detectedObj, index) in detectedObjects.predictions
                            .Select((detected, index) => (detected, index)))
                        {
                            lines.Add($"{index}: Conf: {Math.Round(detectedObj.confidence, 3)} {detectedObj.label}");

                            var rect = Rectangle.FromLTRB(detectedObj.x_min, detectedObj.y_min, 
                                                          detectedObj.x_max, detectedObj.y_max);
                        }

                    detectionResult.Lines = lines.ToArray();

                    Image? image = GetImage(_imageFileName);
                    if (image == null)
                    {
                        ShowError($"Unable to load image {_imageFileName}.");
                        return;
                    }

                    Graphics canvas  = Graphics.FromImage(image);
                    Pen pen          = new Pen(Color.Yellow, 2);
                    SolidBrush brush = new(Color.White);
                    Font drawFont    = new Font("Arial", 10);

                    if (detectedObjects.predictions is not null)
                    {
                        foreach (var (detectedObj, index) in detectedObjects.predictions
                            .Select((detected, index) => (detected, index)))
                        {
                            var rect = Rectangle.FromLTRB(detectedObj.x_min, detectedObj.y_min,
                                                          detectedObj.x_max, detectedObj.y_max);
                            canvas.DrawRectangle(pen, rect);
                            canvas.DrawString($"{index}:{detectedObj.label}", drawFont, brush, 
                                              detectedObj.x_min, detectedObj.y_min);
                        }
                    }

                    SetStatus("Object Detection complete");
                    pictureBox1.Image = image;
                }
                catch (Exception ex)
                {
                    ProcessError(new ErrorResponse (ex.Message));
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

            var result = await _AIService.RegisterFace(UserIdTextbox.Text, _registerFileNames);
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

            float? minConfidence = null;
            if(float.TryParse(MinConfidence.Text, out float parsedConfidence))
                minConfidence = parsedConfidence;

            var result = await _AIService.RecognizeFace(filename, minConfidence);
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

                    Graphics canvas  = Graphics.FromImage(image);
                    Pen pen          = new Pen(Color.Yellow, 2);
                    SolidBrush brush = new(Color.White);
                    Font drawFont    = new Font("Arial", 10);

                    List<string> lines = new();
                    if (recognizeFace.predictions is not null)
                    {
                        var predictionMap = recognizeFace.predictions
                                                         .Select((prediction, Index) => (prediction, Index));
                        foreach (var (prediction, index) in predictionMap)
                        {
                            lines.Add($"{index}: Conf: {Math.Round(prediction.confidence, 3)} {prediction.userid}");
                            var rect = Rectangle.FromLTRB(prediction.x_min, prediction.y_min, 
                                                          prediction.x_max, prediction.y_max);
                            canvas.DrawRectangle(pen, rect);
                            canvas.DrawString($"{index}:{prediction.userid}", drawFont, brush, 
                                              prediction.x_min, prediction.y_min);
                        }

                        detectionResult.Lines = lines.ToArray();
                    }

                    SetStatus("Face recognition complete");
                    pictureBox1.Image = image;
                }
                catch (Exception ex)
                {
                    ProcessError(new ErrorResponse($"{filename} caused: {ex.Message}"));
                }
            }
            else
                ProcessError(result);
        }

        private async void ListFacesBtn_Click(object sender, EventArgs e)
        {
            ClearResults();
            SetStatus("Listing known faces");

            var result = await _AIService.ListRegisteredFaces();
            if (result is ListRegisteredFacesResponse registeredFaces)
            {
                if (result?.success ?? false)
                {
                    List<string> lines = new();
                    if (registeredFaces.faces != null)
                    {
                        var faceMap = registeredFaces.faces.Select((face, Index) => (face, Index));
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

            var result = await _AIService.DeleteRegisteredFace(UserIdTextbox.Text);
            if (result?.success ?? false)
                SetStatus("Completed Face deletion");
            else
                ProcessError(result);
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

        private void ProcessError(ResponseBase? result)
        {
            pictureBox1.Image    = null;
            detectionResult.Text = string.Empty;

            if (result is ErrorResponse response)
                ShowError($"Error: {response.code} - {response.error ?? "No Error Message"}");
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
            var skiaImage = SKImage.FromEncodedData(filename);
            if (skiaImage is null)
                return null;

            return skiaImage.ToBitmap();
        }

        private void OnApiPortChanged(object sender, EventArgs e)
        {
            if (int.TryParse(textApiPort.Text, out int port))
               _AIService.Port = port;
        }

    }
}
