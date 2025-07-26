#!/usr/bin/env python3

import asyncio
import aiohttp
import argparse
import sys
import time
import json
from pathlib import Path
from collections import deque

class StreamingMultipartTest:
    def __init__(self, url: str, image_path: str):
        self.url = url
        self.image_path = Path(image_path)
        self.uploaded_count = 0
        self.results_count = 0
        self.running = True
        self.result_timestamps = deque()
        
        if not self.image_path.exists():
            raise FileNotFoundError(f"Image file not found: {image_path}")
    
    def calculate_rate_per_10s(self):
        """Calculate results per second over 10 second window"""
        now_ms = time.time() * 1000
        window_start = now_ms - 10000
        
        while self.result_timestamps and self.result_timestamps[0] < window_start:
            self.result_timestamps.popleft()
        
        count = len(self.result_timestamps)
        results_per_second = count / 10.0 if count > 0 else 0
        return f"{results_per_second:.1f}/s"
    
    async def display_counters(self):
        """Update counters display continuously"""
        while self.running:
            rate = self.calculate_rate_per_10s()
            print(f"\rUploaded: {self.uploaded_count:6d} | Results: {self.results_count:6d}")
            print(f"Rate (10s): {rate:>10}")
            print("\033[2A", end="", flush=True)
            await asyncio.sleep(0.1)
    
    async def make_single_request(self, session, image_data):
        """Make a single OCR request"""
        try:
            # Create form data for single image
            data = aiohttp.FormData()
            data.add_field('image', image_data, filename=self.image_path.name, content_type='image/jpeg')
            
            # Make request
            async with session.post(self.url, data=data) as response:
                self.uploaded_count += 1
                
                if response.status == 200:
                    # Process response
                    response_text = await response.text()
                    try:
                        # Parse JSON response
                        json_data = json.loads(response_text)
                        # Count results (assuming response is array of OCR results)
                        if isinstance(json_data, list):
                            self.results_count += len(json_data)
                        else:
                            self.results_count += 1
                        
                        # Add timestamp
                        timestamp = time.time() * 1000
                        self.result_timestamps.append(timestamp)
                        
                    except json.JSONDecodeError:
                        # Invalid JSON, still count as processed
                        pass
                        
        except Exception:
            # Ignore all request failures (connection limits, timeouts, etc.)
            pass
    
    async def run_streaming_test(self):
        """Run continuous request loop"""
        try:
            # Load image data once
            with open(self.image_path, 'rb') as f:
                image_data = f.read()
            
            # Single HTTP session
            async with aiohttp.ClientSession() as session:
                while self.running:
                    # Start a new request without waiting for it to complete
                    asyncio.create_task(self.make_single_request(session, image_data))
                    await asyncio.sleep(0.05)  # 50ms
                    
        except Exception:
            # Ignore all errors
            pass
        finally:
            self.running = False
    
    async def run(self):
        """Run the test"""
        print(f"Starting backpressure test...")
        print(f"URL: {self.url}")
        print(f"Image: {self.image_path}")
        print("Press Ctrl+C to stop\n")
        
        # Start display and streaming test tasks
        display_task = asyncio.create_task(self.display_counters())
        test_task = asyncio.create_task(self.run_streaming_test())
        
        try:
            await asyncio.gather(test_task, display_task)
        except (KeyboardInterrupt, asyncio.CancelledError):
            self.running = False
            
        # Clean shutdown
        if not display_task.done():
            display_task.cancel()
        if not test_task.done():
            test_task.cancel()
            
        await asyncio.gather(display_task, test_task, return_exceptions=True)
        
        # Final display
        rate = self.calculate_rate_per_10s()
        print(f"\rUploaded: {self.uploaded_count:6d} | Results: {self.results_count:6d}")
        print(f"Rate (10s): {rate:>10}")
        print("^C")

def main():
    parser = argparse.ArgumentParser(description="Test OCR streaming endpoint backpressure")
    parser.add_argument("--url", required=True, help="Streaming endpoint URL")
    parser.add_argument("--image", required=True, help="Path to image file")
    
    args = parser.parse_args()
    
    try:
        test = StreamingMultipartTest(args.url, args.image)
        asyncio.run(test.run())
    except KeyboardInterrupt:
        print("\n\nShutdown complete.")
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()