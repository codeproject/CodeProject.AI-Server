namespace CodeProject.AI.Demo.Explorer
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                _serverClient?.Dispose();
                _textFont.Dispose();
                _textBrush.Dispose();
                _boundingBoxPen.Dispose();

                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.ImageFileName = new System.Windows.Forms.TextBox();
            this.ImageSelectBtn = new System.Windows.Forms.Button();
            this.Title = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.detectionResult = new System.Windows.Forms.TextBox();
            this.DetectFaceBtn = new System.Windows.Forms.Button();
            this.faceImageSelectBtn1 = new System.Windows.Forms.Button();
            this.FaceImageFileName1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.CompareFacesBtn = new System.Windows.Forms.Button();
            this.Status = new System.Windows.Forms.Label();
            this.DetectSceneBtn = new System.Windows.Forms.Button();
            this.DetectObjectsBtn = new System.Windows.Forms.Button();
            this.RegisterFaceImagesBtn = new System.Windows.Forms.Button();
            this.RecognizeFaceBtn = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.UserIdTextbox = new System.Windows.Forms.TextBox();
            this.SelectRegisterImages = new System.Windows.Forms.Button();
            this.selectFilesDlg = new System.Windows.Forms.OpenFileDialog();
            this.ListFacesBtn = new System.Windows.Forms.Button();
            this.MinConfidence = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.DeleteFaceBtn = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.recognizeImageSelectBtn = new System.Windows.Forms.Button();
            this.RecognizeImageFileName = new System.Windows.Forms.TextBox();
            this.textApiPort = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label7 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.FaceImageFileName2 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.faceImageSelectBtn2 = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.label10 = new System.Windows.Forms.Label();
            this.BenchmarkImageSelectButton = new System.Windows.Forms.Button();
            this.BenchmarkRunCustomBtn = new System.Windows.Forms.Button();
            this.BenchmarkRunStdBtn = new System.Windows.Forms.Button();
            this.BenchmarkFileName = new System.Windows.Forms.TextBox();
            this.BenchmarkResults = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.SuspendLayout();
            // 
            // openFileDialog
            // 
            this.openFileDialog.FileName = "openFileDialog1";
            // 
            // ImageFileName
            // 
            this.ImageFileName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.ImageFileName.Location = new System.Drawing.Point(79, 26);
            this.ImageFileName.Name = "ImageFileName";
            this.ImageFileName.Size = new System.Drawing.Size(203, 23);
            this.ImageFileName.TabIndex = 0;
            this.ImageFileName.TextChanged += new System.EventHandler(this.ImageFileName_TextChanged);
            // 
            // ImageSelectBtn
            // 
            this.ImageSelectBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.ImageSelectBtn.Location = new System.Drawing.Point(288, 24);
            this.ImageSelectBtn.Name = "ImageSelectBtn";
            this.ImageSelectBtn.Size = new System.Drawing.Size(97, 23);
            this.ImageSelectBtn.TabIndex = 1;
            this.ImageSelectBtn.Text = "Select Image";
            this.ImageSelectBtn.UseVisualStyleBackColor = true;
            this.ImageSelectBtn.Click += new System.EventHandler(this.ImageSelect_Click);
            // 
            // Title
            // 
            this.Title.AutoSize = true;
            this.Title.Font = new System.Drawing.Font("Segoe UI", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.Title.Location = new System.Drawing.Point(23, 4);
            this.Title.Name = "Title";
            this.Title.Size = new System.Drawing.Size(231, 30);
            this.Title.TabIndex = 2;
            this.Title.Text = "CodeProject.AI Explorer";
            // 
            // pictureBox1
            // 
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Location = new System.Drawing.Point(432, 217);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(517, 453);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 3;
            this.pictureBox1.TabStop = false;
            // 
            // detectionResult
            // 
            this.detectionResult.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.detectionResult.Location = new System.Drawing.Point(432, 33);
            this.detectionResult.Multiline = true;
            this.detectionResult.Name = "detectionResult";
            this.detectionResult.ReadOnly = true;
            this.detectionResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.detectionResult.Size = new System.Drawing.Size(517, 178);
            this.detectionResult.TabIndex = 4;
            // 
            // DetectFaceBtn
            // 
            this.DetectFaceBtn.Enabled = false;
            this.DetectFaceBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.DetectFaceBtn.Location = new System.Drawing.Point(81, 53);
            this.DetectFaceBtn.Name = "DetectFaceBtn";
            this.DetectFaceBtn.Size = new System.Drawing.Size(97, 23);
            this.DetectFaceBtn.TabIndex = 5;
            this.DetectFaceBtn.Text = "Detect Faces";
            this.DetectFaceBtn.UseVisualStyleBackColor = true;
            this.DetectFaceBtn.Click += new System.EventHandler(this.DetectFaceBtn_Click);
            // 
            // faceImageSelectBtn1
            // 
            this.faceImageSelectBtn1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.faceImageSelectBtn1.Location = new System.Drawing.Point(231, 24);
            this.faceImageSelectBtn1.Name = "faceImageSelectBtn1";
            this.faceImageSelectBtn1.Size = new System.Drawing.Size(85, 23);
            this.faceImageSelectBtn1.TabIndex = 7;
            this.faceImageSelectBtn1.Text = "Select Image";
            this.faceImageSelectBtn1.UseVisualStyleBackColor = true;
            this.faceImageSelectBtn1.Click += new System.EventHandler(this.FaceImage1Select_Click);
            // 
            // FaceImageFileName1
            // 
            this.FaceImageFileName1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.FaceImageFileName1.Location = new System.Drawing.Point(74, 24);
            this.FaceImageFileName1.Name = "FaceImageFileName1";
            this.FaceImageFileName1.Size = new System.Drawing.Size(157, 23);
            this.FaceImageFileName1.TabIndex = 6;
            this.FaceImageFileName1.TextChanged += new System.EventHandler(this.FaceImageFileName1_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(35, 27);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(40, 15);
            this.label1.TabIndex = 8;
            this.label1.Text = "Image";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label2.Location = new System.Drawing.Point(26, 28);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 15);
            this.label2.TabIndex = 8;
            this.label2.Text = "Face 1";
            // 
            // CompareFacesBtn
            // 
            this.CompareFacesBtn.Enabled = false;
            this.CompareFacesBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.CompareFacesBtn.Location = new System.Drawing.Point(322, 35);
            this.CompareFacesBtn.Name = "CompareFacesBtn";
            this.CompareFacesBtn.Size = new System.Drawing.Size(67, 23);
            this.CompareFacesBtn.TabIndex = 9;
            this.CompareFacesBtn.Text = "Compare";
            this.CompareFacesBtn.UseVisualStyleBackColor = true;
            this.CompareFacesBtn.Click += new System.EventHandler(this.CompareFacesBtn_Click);
            // 
            // Status
            // 
            this.Status.AutoEllipsis = true;
            this.Status.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.Status.ForeColor = System.Drawing.Color.DarkOrange;
            this.Status.Location = new System.Drawing.Point(474, 9);
            this.Status.Name = "Status";
            this.Status.Size = new System.Drawing.Size(288, 21);
            this.Status.TabIndex = 12;
            this.Status.Text = "Searching for API server...";
            // 
            // DetectSceneBtn
            // 
            this.DetectSceneBtn.Enabled = false;
            this.DetectSceneBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.DetectSceneBtn.Location = new System.Drawing.Point(183, 53);
            this.DetectSceneBtn.Name = "DetectSceneBtn";
            this.DetectSceneBtn.Size = new System.Drawing.Size(97, 23);
            this.DetectSceneBtn.TabIndex = 5;
            this.DetectSceneBtn.Text = "Detect Scene";
            this.DetectSceneBtn.UseVisualStyleBackColor = true;
            this.DetectSceneBtn.Click += new System.EventHandler(this.DetectSceneBtn_Click);
            // 
            // DetectObjectsBtn
            // 
            this.DetectObjectsBtn.Enabled = false;
            this.DetectObjectsBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.DetectObjectsBtn.Location = new System.Drawing.Point(288, 53);
            this.DetectObjectsBtn.Name = "DetectObjectsBtn";
            this.DetectObjectsBtn.Size = new System.Drawing.Size(97, 23);
            this.DetectObjectsBtn.TabIndex = 5;
            this.DetectObjectsBtn.Text = "Detect Objects";
            this.DetectObjectsBtn.UseVisualStyleBackColor = true;
            this.DetectObjectsBtn.Click += new System.EventHandler(this.DetectObjectsBtn_Click);
            // 
            // RegisterFaceImagesBtn
            // 
            this.RegisterFaceImagesBtn.Enabled = false;
            this.RegisterFaceImagesBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.RegisterFaceImagesBtn.Location = new System.Drawing.Point(23, 70);
            this.RegisterFaceImagesBtn.Name = "RegisterFaceImagesBtn";
            this.RegisterFaceImagesBtn.Size = new System.Drawing.Size(169, 23);
            this.RegisterFaceImagesBtn.TabIndex = 5;
            this.RegisterFaceImagesBtn.Text = "3. Register Images";
            this.RegisterFaceImagesBtn.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.RegisterFaceImagesBtn.UseVisualStyleBackColor = true;
            this.RegisterFaceImagesBtn.Click += new System.EventHandler(this.RegisterFaceBtn_Click);
            // 
            // RecognizeFaceBtn
            // 
            this.RecognizeFaceBtn.Enabled = false;
            this.RecognizeFaceBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.RecognizeFaceBtn.Location = new System.Drawing.Point(317, 22);
            this.RecognizeFaceBtn.Name = "RecognizeFaceBtn";
            this.RecognizeFaceBtn.Size = new System.Drawing.Size(74, 23);
            this.RecognizeFaceBtn.TabIndex = 5;
            this.RecognizeFaceBtn.Text = "Recognize";
            this.RecognizeFaceBtn.UseVisualStyleBackColor = true;
            this.RecognizeFaceBtn.Click += new System.EventHandler(this.RecognizeFaceBtn_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label4.Location = new System.Drawing.Point(23, 23);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(98, 15);
            this.label4.TabIndex = 14;
            this.label4.Text = "1. Person\'s Name";
            // 
            // UserIdTextbox
            // 
            this.UserIdTextbox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.UserIdTextbox.Location = new System.Drawing.Point(124, 20);
            this.UserIdTextbox.Name = "UserIdTextbox";
            this.UserIdTextbox.Size = new System.Drawing.Size(68, 23);
            this.UserIdTextbox.TabIndex = 15;
            this.UserIdTextbox.TextChanged += new System.EventHandler(this.OnUserIdTextboxChanged);
            // 
            // SelectRegisterImages
            // 
            this.SelectRegisterImages.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.SelectRegisterImages.Location = new System.Drawing.Point(23, 45);
            this.SelectRegisterImages.Name = "SelectRegisterImages";
            this.SelectRegisterImages.Size = new System.Drawing.Size(169, 23);
            this.SelectRegisterImages.TabIndex = 7;
            this.SelectRegisterImages.Text = "2. Select Images";
            this.SelectRegisterImages.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.SelectRegisterImages.UseVisualStyleBackColor = true;
            this.SelectRegisterImages.Click += new System.EventHandler(this.SelectRegisterImages_Click);
            // 
            // selectFilesDlg
            // 
            this.selectFilesDlg.FileName = "selectFilesDlg";
            this.selectFilesDlg.Multiselect = true;
            // 
            // ListFacesBtn
            // 
            this.ListFacesBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.ListFacesBtn.Location = new System.Drawing.Point(228, 41);
            this.ListFacesBtn.Name = "ListFacesBtn";
            this.ListFacesBtn.Size = new System.Drawing.Size(133, 23);
            this.ListFacesBtn.TabIndex = 5;
            this.ListFacesBtn.Text = "List Registered Faces";
            this.ListFacesBtn.UseVisualStyleBackColor = true;
            this.ListFacesBtn.Click += new System.EventHandler(this.ListFacesBtn_Click);
            // 
            // MinConfidence
            // 
            this.MinConfidence.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.MinConfidence.Location = new System.Drawing.Point(228, 51);
            this.MinConfidence.Name = "MinConfidence";
            this.MinConfidence.Size = new System.Drawing.Size(55, 23);
            this.MinConfidence.TabIndex = 16;
            this.MinConfidence.Text = "0.6";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label5.Location = new System.Drawing.Point(35, 54);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(186, 15);
            this.label5.TabIndex = 17;
            this.label5.Text = "Minimum recognition confidence";
            // 
            // DeleteFaceBtn
            // 
            this.DeleteFaceBtn.BackColor = System.Drawing.Color.MistyRose;
            this.DeleteFaceBtn.Enabled = false;
            this.DeleteFaceBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.DeleteFaceBtn.ForeColor = System.Drawing.Color.Red;
            this.DeleteFaceBtn.Location = new System.Drawing.Point(264, 70);
            this.DeleteFaceBtn.Name = "DeleteFaceBtn";
            this.DeleteFaceBtn.Size = new System.Drawing.Size(97, 23);
            this.DeleteFaceBtn.TabIndex = 5;
            this.DeleteFaceBtn.Text = "Delete Person";
            this.DeleteFaceBtn.UseVisualStyleBackColor = false;
            this.DeleteFaceBtn.Click += new System.EventHandler(this.DeleteFaceBtn_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label6.Location = new System.Drawing.Point(14, 57);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(60, 15);
            this.label6.TabIndex = 18;
            this.label6.Text = "Operation";
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.groupBox1.Controls.Add(this.SelectRegisterImages);
            this.groupBox1.Controls.Add(this.UserIdTextbox);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.RegisterFaceImagesBtn);
            this.groupBox1.Controls.Add(this.DeleteFaceBtn);
            this.groupBox1.Controls.Add(this.ListFacesBtn);
            this.groupBox1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.groupBox1.Location = new System.Drawing.Point(26, 241);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(1);
            this.groupBox1.Size = new System.Drawing.Size(392, 98);
            this.groupBox1.TabIndex = 19;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Face Registration";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label9.Location = new System.Drawing.Point(6, 25);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(40, 15);
            this.label9.TabIndex = 21;
            this.label9.Text = "Image";
            // 
            // recognizeImageSelectBtn
            // 
            this.recognizeImageSelectBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.recognizeImageSelectBtn.Location = new System.Drawing.Point(228, 22);
            this.recognizeImageSelectBtn.Name = "recognizeImageSelectBtn";
            this.recognizeImageSelectBtn.Size = new System.Drawing.Size(85, 23);
            this.recognizeImageSelectBtn.TabIndex = 20;
            this.recognizeImageSelectBtn.Text = "Select Image";
            this.recognizeImageSelectBtn.UseVisualStyleBackColor = true;
            this.recognizeImageSelectBtn.Click += new System.EventHandler(this.RecognizeImageSelect_Click);
            // 
            // RecognizeImageFileName
            // 
            this.RecognizeImageFileName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.RecognizeImageFileName.Location = new System.Drawing.Point(51, 22);
            this.RecognizeImageFileName.Name = "RecognizeImageFileName";
            this.RecognizeImageFileName.Size = new System.Drawing.Size(177, 23);
            this.RecognizeImageFileName.TabIndex = 19;
            this.RecognizeImageFileName.TextChanged += new System.EventHandler(this.RecognizeImageFileName_TextChanged);
            // 
            // textApiPort
            // 
            this.textApiPort.Location = new System.Drawing.Point(370, 31);
            this.textApiPort.Name = "textApiPort";
            this.textApiPort.Size = new System.Drawing.Size(56, 23);
            this.textApiPort.TabIndex = 21;
            this.textApiPort.Text = "32168";
            this.textApiPort.TextChanged += new System.EventHandler(this.OnApiPortChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(335, 34);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(29, 15);
            this.label3.TabIndex = 20;
            this.label3.Text = "Port";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label7.Location = new System.Drawing.Point(432, 9);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(39, 15);
            this.label7.TabIndex = 22;
            this.label7.Text = "Status";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.FaceImageFileName2);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.faceImageSelectBtn2);
            this.groupBox2.Controls.Add(this.CompareFacesBtn);
            this.groupBox2.Controls.Add(this.FaceImageFileName1);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.faceImageSelectBtn1);
            this.groupBox2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.groupBox2.Location = new System.Drawing.Point(23, 155);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(1);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(1);
            this.groupBox2.Size = new System.Drawing.Size(394, 82);
            this.groupBox2.TabIndex = 23;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Face Comparison";
            // 
            // FaceImageFileName2
            // 
            this.FaceImageFileName2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.FaceImageFileName2.Location = new System.Drawing.Point(74, 49);
            this.FaceImageFileName2.Name = "FaceImageFileName2";
            this.FaceImageFileName2.Size = new System.Drawing.Size(157, 23);
            this.FaceImageFileName2.TabIndex = 10;
            this.FaceImageFileName2.TextChanged += new System.EventHandler(this.FaceImageFileName2_TextChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label8.Location = new System.Drawing.Point(26, 53);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(40, 15);
            this.label8.TabIndex = 12;
            this.label8.Text = "Face 2";
            // 
            // faceImageSelectBtn2
            // 
            this.faceImageSelectBtn2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.faceImageSelectBtn2.Location = new System.Drawing.Point(231, 49);
            this.faceImageSelectBtn2.Name = "faceImageSelectBtn2";
            this.faceImageSelectBtn2.Size = new System.Drawing.Size(85, 23);
            this.faceImageSelectBtn2.TabIndex = 11;
            this.faceImageSelectBtn2.Text = "Select Image";
            this.faceImageSelectBtn2.UseVisualStyleBackColor = true;
            this.faceImageSelectBtn2.Click += new System.EventHandler(this.FaceImage2Select_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.ImageFileName);
            this.groupBox3.Controls.Add(this.ImageSelectBtn);
            this.groupBox3.Controls.Add(this.DetectFaceBtn);
            this.groupBox3.Controls.Add(this.DetectSceneBtn);
            this.groupBox3.Controls.Add(this.DetectObjectsBtn);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.groupBox3.Location = new System.Drawing.Point(26, 58);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(1);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(1);
            this.groupBox3.Size = new System.Drawing.Size(394, 91);
            this.groupBox3.TabIndex = 24;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Object Detection";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label9);
            this.groupBox4.Controls.Add(this.recognizeImageSelectBtn);
            this.groupBox4.Controls.Add(this.RecognizeFaceBtn);
            this.groupBox4.Controls.Add(this.RecognizeImageFileName);
            this.groupBox4.Controls.Add(this.MinConfidence);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.groupBox4.Location = new System.Drawing.Point(26, 351);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(394, 88);
            this.groupBox4.TabIndex = 25;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Face Recognition";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.label10);
            this.groupBox5.Controls.Add(this.BenchmarkImageSelectButton);
            this.groupBox5.Controls.Add(this.BenchmarkRunCustomBtn);
            this.groupBox5.Controls.Add(this.BenchmarkRunStdBtn);
            this.groupBox5.Controls.Add(this.BenchmarkFileName);
            this.groupBox5.Controls.Add(this.BenchmarkResults);
            this.groupBox5.Controls.Add(this.label11);
            this.groupBox5.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.groupBox5.Location = new System.Drawing.Point(26, 456);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(394, 107);
            this.groupBox5.TabIndex = 25;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Benchmark";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label10.Location = new System.Drawing.Point(6, 25);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(40, 15);
            this.label10.TabIndex = 21;
            this.label10.Text = "Image";
            // 
            // BenchmarkImageSelectButton
            // 
            this.BenchmarkImageSelectButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.BenchmarkImageSelectButton.Location = new System.Drawing.Point(288, 21);
            this.BenchmarkImageSelectButton.Name = "BenchmarkImageSelectButton";
            this.BenchmarkImageSelectButton.Size = new System.Drawing.Size(85, 23);
            this.BenchmarkImageSelectButton.TabIndex = 20;
            this.BenchmarkImageSelectButton.Text = "Select Image";
            this.BenchmarkImageSelectButton.UseVisualStyleBackColor = true;
            this.BenchmarkImageSelectButton.Click += new System.EventHandler(this.benchmarkImageSelectButton_Click);
            // 
            // BenchmarkRunCustomBtn
            // 
            this.BenchmarkRunCustomBtn.Enabled = false;
            this.BenchmarkRunCustomBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.BenchmarkRunCustomBtn.Location = new System.Drawing.Point(309, 51);
            this.BenchmarkRunCustomBtn.Name = "BenchmarkRunCustomBtn";
            this.BenchmarkRunCustomBtn.Size = new System.Drawing.Size(74, 23);
            this.BenchmarkRunCustomBtn.TabIndex = 5;
            this.BenchmarkRunCustomBtn.Text = "Custom";
            this.BenchmarkRunCustomBtn.UseVisualStyleBackColor = true;
            this.BenchmarkRunCustomBtn.Click += new System.EventHandler(this.BenchmarkRunCustomBtn_Click);
            // 
            // BenchmarkRunStdBtn
            // 
            this.BenchmarkRunStdBtn.Enabled = false;
            this.BenchmarkRunStdBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.BenchmarkRunStdBtn.Location = new System.Drawing.Point(228, 51);
            this.BenchmarkRunStdBtn.Name = "BenchmarkRunStdBtn";
            this.BenchmarkRunStdBtn.Size = new System.Drawing.Size(74, 23);
            this.BenchmarkRunStdBtn.TabIndex = 5;
            this.BenchmarkRunStdBtn.Text = "Standard";
            this.BenchmarkRunStdBtn.UseVisualStyleBackColor = true;
            this.BenchmarkRunStdBtn.Click += new System.EventHandler(this.BenchmarkRunStdBtn_Click);
            // 
            // BenchmarkFileName
            // 
            this.BenchmarkFileName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.BenchmarkFileName.Location = new System.Drawing.Point(51, 22);
            this.BenchmarkFileName.Name = "BenchmarkFileName";
            this.BenchmarkFileName.Size = new System.Drawing.Size(229, 23);
            this.BenchmarkFileName.TabIndex = 19;
            this.BenchmarkFileName.TextChanged += new System.EventHandler(this.BenchFileName_TextChanged);
            // 
            // BenchmarkResults
            // 
            this.BenchmarkResults.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.BenchmarkResults.Location = new System.Drawing.Point(51, 75);
            this.BenchmarkResults.Name = "BenchmarkResults";
            this.BenchmarkResults.ReadOnly = true;
            this.BenchmarkResults.Size = new System.Drawing.Size(334, 23);
            this.BenchmarkResults.TabIndex = 16;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label11.Location = new System.Drawing.Point(7, 78);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(39, 15);
            this.label11.TabIndex = 17;
            this.label11.Text = "Result";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(961, 682);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.textApiPort);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.Status);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.detectionResult);
            this.Controls.Add(this.Title);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "CodeProject.AI Explorer";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.TextBox ImageFileName;
        private System.Windows.Forms.Button ImageSelectBtn;
        private System.Windows.Forms.Label Title;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.TextBox detectionResult;
        private System.Windows.Forms.Button DetectFaceBtn;
        private System.Windows.Forms.Button faceImageSelectBtn1;
        private System.Windows.Forms.TextBox FaceImageFileName1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button CompareFacesBtn;
        private System.Windows.Forms.Label Status;
        private System.Windows.Forms.Button DetectSceneBtn;
        private System.Windows.Forms.Button DetectObjectsBtn;
        private System.Windows.Forms.Button RegisterFaceImagesBtn;
        private System.Windows.Forms.Button RecognizeFaceBtn;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button SelectRegisterImages;
        private System.Windows.Forms.OpenFileDialog selectFilesDlg;
        private System.Windows.Forms.Button ListFacesBtn;
        private System.Windows.Forms.TextBox MinConfidence;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button DeleteFaceBtn;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textApiPort;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button recognizeImageSelectBtn;
        private System.Windows.Forms.TextBox RecognizeImageFileName;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox UserIdTextbox;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.TextBox FaceImageFileName2;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button faceImageSelectBtn2;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Button BenchmarkImageSelectButton;
        private System.Windows.Forms.Button BenchmarkRunStdBtn;
        private System.Windows.Forms.TextBox BenchmarkFileName;
        private System.Windows.Forms.TextBox BenchmarkResults;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Button BenchmarkRunCustomBtn;
    }
}

