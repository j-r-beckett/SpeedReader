#!/usr/bin/env python3
import json
import sys
import os

def replace_padToWidth_with_pad(pipeline_file):
    """
    Replace PadToWidth with Pad transform in the pipeline.json file
    """
    print(f"Processing: {pipeline_file}")

    try:
        with open(pipeline_file, 'r') as f:
            pipeline_data = json.load(f)

        # Check if this is the expected structure
        if 'pipeline' not in pipeline_data or 'tasks' not in pipeline_data['pipeline']:
            print("Warning: Unexpected pipeline.json structure")
            return False

        # Look for the transform task
        modified = False
        for task in pipeline_data['pipeline']['tasks']:
            if task.get('type') == 'Task' and task.get('module') == 'Transform' and 'transforms' in task:
                # Find PadToWidth in transforms list
                for i, transform in enumerate(task['transforms']):
                    if transform.get('type') == 'PadToWidth':
                        print(f"Found PadToWidth transform with width={transform.get('width', 0)}")

                        # Get the width parameter
                        width = transform.get('width', 0)
                        pad_val = transform.get('pad_val', 0)

                        # Create new Pad transform
                        new_transform = {
                            "type": "Pad",
                            "padding": [0, width, 0, 0],  # [top, right, bottom, left]
                            "padding_mode": "constant",
                            "pad_val": pad_val
                        }

                        # Replace the transform
                        task['transforms'][i] = new_transform
                        print(f"Replaced with Pad transform: padding=[0, {width}, 0, 0]")
                        modified = True

        if modified:
            # Write back the modified pipeline
            with open(pipeline_file, 'w') as f:
                json.dump(pipeline_data, f, indent=2)
            print(f"Successfully updated {pipeline_file}")
            return True
        else:
            print(f"No PadToWidth transform found in {pipeline_file}")
            return False

    except Exception as e:
        print(f"Error processing {pipeline_file}: {str(e)}")
        return False

def main():
    # Check if a file was provided as an argument
    if len(sys.argv) < 2:
        print("Usage: python replace_padToWidth_with_pad.py /path/to/pipeline.json")
        return

    pipeline_file = sys.argv[1]

    # Check if the file exists
    if not os.path.exists(pipeline_file):
        print(f"Error: File {pipeline_file} does not exist")
        return

    # Process the pipeline.json file
    success = replace_padToWidth_with_pad(pipeline_file)

    if success:
        print("Transform replacement completed successfully")
    else:
        print("Transform replacement failed or not needed")

if __name__ == "__main__":
    main()
