const { VertexAI } = require('@google-cloud/vertexai');

async function testModel(modelName, location) {
  try {
    const vertex_ai = new VertexAI({project: 'project-263b6139-6893-4788-995', location: location});
    const generativeModel = vertex_ai.preview.getGenerativeModel({
      model: modelName,
    });
    
    const resp = await generativeModel.generateContent({
      contents: [{role: 'user', parts: [{text: 'Respond with the word SUCCESS'}]}],
    });
    console.log(`[PASS] ${modelName} in ${location}: ${resp.response.candidates[0].content.parts[0].text}`);
    return true;
  } catch (error) {
    console.log(`[FAIL] ${modelName} in ${location}: ${error.message}`);
    return false;
  }
}

async function runTests() {
  const models = [
    'gemini-3.1-pro-preview',
    'gemini-3.1-pro',
  ];
  const locations = ['global', 'us-central1'];

  for (const model of models) {
    for (const loc of locations) {
      await testModel(model, loc);
    }
  }
}

runTests();
