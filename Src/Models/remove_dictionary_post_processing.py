#!/usr/bin/env python3
import json
import sys
import os

def remove_dictionary_post_processing(pipeline_file):
    """
    Modify the Dictionary-based AttnConvertor to use an empty dictionary
    """
    print(f"Processing: {pipeline_file}")

    try:
        with open(pipeline_file, 'r') as f:
            pipeline_data = json.load(f)

        # Check if this is the expected structure
        if 'pipeline' not in pipeline_data or 'tasks' not in pipeline_data['pipeline']:
            print("Warning: Unexpected pipeline.json structure")
            return False

        # Look for the AttnConvertor task
        modified = False
        for task in pipeline_data['pipeline']['tasks']:
            if (task.get('type') == 'Task' and
                task.get('module') == 'mmocr' and
                task.get('component') == 'AttnConvertor' and
                'params' in task):

                print(f"Found AttnConvertor component")

                # Get the directory of the pipeline file to save the new dict file
                pipeline_dir = os.path.dirname(os.path.abspath(pipeline_file))
                empty_dict_path = os.path.join(pipeline_dir, 'empty_dict.txt')

                # Create an empty dictionary file
                print(f"Creating empty dictionary file at {empty_dict_path}")
                with open(empty_dict_path, 'w') as dict_file:
                    # Add just a space character to ensure file validity
                    dict_file.write(" \n")

                # Update the params to use our empty dictionary
                task['params'] = {
                    "type": "Dictionary",
                    "dict_file": "empty_dict.txt",  # Use the empty dictionary file
                    "with_unknown": True,  # Critical to handle characters not in dictionary
                    "max_seq_len": 40,
                    "lower": False,
                    "with_start": False,
                    "with_end": False,
                    "with_padding": True,  # Keep padding for model compatibility
                    "same_start_end": False,
                    "ignore_chars": ["padding"]
                }

                print("Modified to use empty dictionary file")
                modified = True
                break

        if modified:
            # Write back the modified pipeline
            with open(pipeline_file, 'w') as f:
                json.dump(pipeline_data, f, indent=2)
            print(f"Successfully updated {pipeline_file}")
            return True
        else:
            print(f"No AttnConvertor found in {pipeline_file}")
            return False

    except Exception as e:
        print(f"Error processing {pipeline_file}: {str(e)}")
        return False

def main():
    # Check if a file was provided as an argument
    if len(sys.argv) < 2:
        print("Usage: python remove_dictionary_post_processing.py /path/to/pipeline.json")
        return

    pipeline_file = sys.argv[1]

    # Check if the file exists
    if not os.path.exists(pipeline_file):
        print(f"Error: File {pipeline_file} does not exist")
        return

    # Process the pipeline.json file
    success = remove_dictionary_post_processing(pipeline_file)

    if success:
        print("Dictionary post-processing removal completed successfully")
    else:
        print("Dictionary post-processing removal failed or not needed")

if __name__ == "__main__":
    main()
