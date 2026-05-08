const https = require('https');
const fs = require('fs');

async function runTest() {
  console.log("--- Local Jules API Test Script ---");

  const apiKey = process.env.JULES_API_KEY;
  if (!apiKey) {
    console.error("ERROR: Please set the JULES_API_KEY environment variable before running.");
    console.error(`Example (PowerShell): $env:JULES_API_KEY="your_key"`);
    process.exit(1);
  }

  // Use a hardcoded test repo and branch since we aren't in a real GH Action
  const repo = "mkalhitti-cloud/universal-or-strategy";
  const branch = "main"; // Change to a valid branch if needed
  const prTitle = "Local API Test";

  const prompt = `Perform a forensic logic audit of PR "${prTitle}" on branch "${branch}". Rules: 1. Lock-Free Actor Pattern (Enqueue). 2. ASCII-Only strings. Post findings as a summary.`;

  const triggerData = JSON.stringify({
    prompt: prompt,
    sourceContext: {
      source: `sources/github/${repo}`,
      githubRepoContext: { startingBranch: branch }
    },
    title: `Audit: ${prTitle}`
  });

  const triggerOptions = {
    hostname: 'jules.googleapis.com',
    path: '/v1alpha/sessions',
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-goog-api-key': apiKey
    }
  };

  let sessionName = '';
  let sessionUrl = '';

  console.log(`Triggering Jules session for ${repo}...`);
  try {
    const result = await new Promise((resolve, reject) => {
      const req = https.request(triggerOptions, (res) => {
        let body = '';
        res.on('data', (chunk) => body += chunk);
        res.on('end', () => {
          if (res.statusCode >= 200 && res.statusCode < 300) {
            try {
              const data = JSON.parse(body);
              resolve({ name: data.name, url: data.url });
            } catch (e) {
              reject(new Error(`Failed to parse response: ${body}`));
            }
          } else {
            reject(new Error(`Trigger failed (${res.statusCode}): ${body}`));
          }
        });
      });
      req.on('error', reject);
      req.write(triggerData);
      req.end();
    });
    sessionName = result.name;
    sessionUrl = result.url;
    console.log(`✅ Session created: ${sessionName}`);
    console.log(`🔗 URL: ${sessionUrl}`);
  } catch (e) {
    console.error("❌ " + e.message);
    process.exit(1);
  }

  // Ensure sessionName doesn't already have a leading slash
  const cleanSessionName = sessionName.replace(/^\//, '');

  // Polling Logic via Activities Endpoint
  const pollOptions = {
    hostname: 'jules.googleapis.com',
    path: `/v1alpha/${cleanSessionName}/activities?pageSize=100`,
    method: 'GET',
    headers: { 'x-goog-api-key': apiKey }
  };

  let finished = false;
  let isFailed = false;
  let finalSummary = "Audit complete. Check session URL for details.";
  let attempts = 0;
  const maxAttempts = 5; // Just testing locally, don't wait an hour

  console.log("\nPolling for completion (Max 5 attempts for local test)...");
  console.log(`Polling URL: https://${pollOptions.hostname}${pollOptions.path}`);

  while (!finished && attempts < maxAttempts) {
    attempts++;
    process.stdout.write(`Attempt ${attempts}... `);
    
    const activitiesData = await new Promise((resolve) => {
      https.get(pollOptions, (res) => {
        let body = '';
        res.on('data', (chunk) => body += chunk);
        res.on('end', () => {
           try {
             resolve(JSON.parse(body));
           } catch(e) {
             resolve({ error: "Failed to parse API response", body });
           }
        });
      });
    });

    if (activitiesData && activitiesData.activities) {
      console.log(`Found ${activitiesData.activities.length} activities.`);
      
      // Check for completion markers
      const completedAct = activitiesData.activities.find(a => a.sessionCompleted);
      const failedAct = activitiesData.activities.find(a => a.sessionFailed || (a.progressUpdated && a.progressUpdated.title && a.progressUpdated.title.toLowerCase().includes('failed')));
      
      if (completedAct) {
        finished = true;
        console.log(`\n✅ Session state: COMPLETED`);
        break;
      } else if (failedAct) {
        finished = true;
        isFailed = true;
        console.log(`\n❌ Session state: FAILED`);
        break;
      }
    } else {
       console.log("No activities found yet, or error:", activitiesData);
    }

    if (!finished && attempts < maxAttempts) {
      // Poll faster for local testing just to verify the endpoint is working
      await new Promise(r => setTimeout(r, 10000));
    }
  }

  if (!finished) {
    console.error('\n⚠️ Local test timed out (reached 5 attempts). The connection works, but the AI is still thinking.');
    process.exit(0);
  }
}

runTest();
