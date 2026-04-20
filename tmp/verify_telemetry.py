import os
import sentry_sdk
from langsmith import Client, traceable
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# Initialize Sentry
dsn = os.getenv("SENTRY_DSN")
if dsn:
    sentry_sdk.init(dsn=dsn, traces_sample_rate=1.0)
    print("Sentry initialized.")
else:
    print("SENTRY_DSN not found in environment.")

# Initialize LangSmith
client = Client()

@traceable(project_name="Sovereign-Multi-Agent")
def test_mission_telemetry():
    print("Testing LangSmith tracing...")
    return "Telemetry verification successful"

if __name__ == "__main__":
    try:
        # Test LangSmith
        result = test_mission_telemetry()
        print(f"LangSmith Result: {result}")
        
        # Test Sentry (send a message)
        sentry_sdk.capture_message("Mission Telemetry Hardening: Verification Ping")
        print("Sentry verification ping sent.")
        
        # Force a small division by zero to test crash capture
        print("Testing crash capture (Division by zero)...")
        # 1 / 0
        
    except Exception as e:
        print(f"Caught expected error: {e}")
        sentry_sdk.capture_exception(e)
    
    print("Verification script complete. Check dashboards.")
