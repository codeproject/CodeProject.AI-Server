const axios = require('axios');
const FormData = require('form-data');
const fs = require('fs');
const path = require('path');

const API_URL = 'http://localhost:32168/v1/vision/detection';
const IMAGE_PATH = '..\\..\\..\\TestData\\Objects\\study-group.jpg';


async function detectObjects() {

    var image_path = path.resolve(process.cwd(), IMAGE_PATH);

    const form = new FormData();
    form.append('image', fs.createReadStream(image_path));

    try {
        const response = await axios.post(API_URL, form, {
            headers: {
                ...form.getHeaders(),
            },
        });

        const { predictions } = response.data;
        predictions.forEach(prediction => {
            console.log(`Object: ${prediction.label}, Confidence: ${prediction.confidence.toFixed(2)},` +
                        `Bounding Box: [${prediction.x_min}, ${prediction.y_min}, ${prediction.x_max}, ${prediction.y_max}]`);
        });
    } catch (error) {
        console.error('Error detecting objects:', error);
    }
}

detectObjects();
