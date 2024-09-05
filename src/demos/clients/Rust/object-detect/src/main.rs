use reqwest::multipart;
use serde::Deserialize;
use std::fs::File;
use std::io::Read;


// NOTE: You need to ensure the Rust toolchain is all present and accounted for.
// In a terminal:
//    rustup toolchain install stable-x86_64-pc-windows-gnu
//    rustup default stable-x86_64-pc-windows-gnu

const API_URL: &str = "http://localhost:32168/v1/vision/detection";
const IMAGE_PATH: &str = "..\\..\\..\\..\\..\\TestData\\Objects\\study-group.jpg";

#[derive(Debug, Deserialize)]
struct Prediction {
    label: String,
    confidence: f64,
    x_min: i32,
    y_min: i32,
    x_max: i32,
    y_max: i32,
}

#[derive(Debug, Deserialize)]
struct DetectionResponse {
    predictions: Vec<Prediction>,
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Open the image file
    let mut file = File::open(IMAGE_PATH)?;
    let mut buffer = Vec::new();
    file.read_to_end(&mut buffer)?;

    // Create a multipart form
    let part = multipart::Part::bytes(buffer)
        .file_name("image.jpg")
        .mime_str("image/jpeg")?;

    let form = multipart::Form::new()
        .part("image", part);

    // Make the HTTP request to the CodeProject.AI Server
    let client = reqwest::Client::new();
    let response = client.post(API_URL)
        .multipart(form)
        .send()
        .await?;

    // Parse the JSON response
    let detection_response: DetectionResponse = response.json().await?;

    // Print the detected objects
    for prediction in detection_response.predictions {
        println!(
            "Object: {}, Confidence: {:.2}, Bounding Box: [{}, {}, {}, {}]",
            prediction.label, prediction.confidence, prediction.x_min, prediction.y_min, prediction.x_max, prediction.y_max
        );
    }

    Ok(())
}
