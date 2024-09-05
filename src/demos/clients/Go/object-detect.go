package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"mime/multipart"
	"net/http"
	"os"
)

const (
	apiUrl    = "http://localhost:32168/v1/vision/detection"
	imagePath = "..\\..\\TestData\\Objects\\study-group.jpg"
)

type DetectionResponse struct {
	Predictions []struct {
		Label string  `json:"label"`
		Score float64 `json:"confidence"`
		XMin  int     `json:"x_min"`
		YMin  int     `json:"y_min"`
		XMax  int     `json:"x_max"`
		YMax  int     `json:"y_max"`
	} `json:"predictions"`
}

func main() {
	// Open the image file
	file, err := os.Open(imagePath)
	if err != nil {
		log.Fatalf("failed to open image: %v", err)
	}
	defer file.Close()

	// Create a buffer to hold the multipart form data
	body := &bytes.Buffer{}
	writer := multipart.NewWriter(body)

	// Add the image file to the form data
	part, err := writer.CreateFormFile("image", imagePath)
	if err != nil {
		log.Fatalf("failed to create form file: %v", err)
	}
	_, err = io.ReadAll(file)
	if err != nil {
		log.Fatalf("failed to read image file: %v", err)
	}

	file.Seek(0, 0)
	_, err = io.Copy(part, file)
	if err != nil {
		log.Fatalf("failed to copy file content: %v", err)
	}

	// Close the writer to set the terminating boundary
	err = writer.Close()
	if err != nil {
		log.Fatalf("failed to close writer: %v", err)
	}

	// Make the HTTP request to the CodeProject.AI Server
	req, err := http.NewRequest("POST", apiUrl, body)
	if err != nil {
		log.Fatalf("failed to create request: %v", err)
	}
	req.Header.Set("Content-Type", writer.FormDataContentType())

	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		log.Fatalf("failed to perform request: %v", err)
	}
	defer resp.Body.Close()

	// Read the response
	responseBody, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Fatalf("failed to read response body: %v", err)
	}

	// Parse the JSON response
	var detectionResponse DetectionResponse
	err = json.Unmarshal(responseBody, &detectionResponse)
	if err != nil {
		log.Fatalf("failed to unmarshal response: %v", err)
	}

	// Print the detected objects
	for _, prediction := range detectionResponse.Predictions {
		fmt.Printf("Object: %s, Confidence: %.2f, Bounding Box: [%d, %d, %d, %d]\n",
			prediction.Label, prediction.Score, prediction.XMin, prediction.YMin, prediction.XMax, prediction.YMax)
	}
}
