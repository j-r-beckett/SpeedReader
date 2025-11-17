target/
  models/
    onnx/  # copied from /models
      dbnet_resnet18_fp32.onnx
      dbnet_resnet18_int8.onnx
      svtrv2_fp32.onnx
    build/  # shallow clones of repos
      mmdeploy/
      mmocr/
      openocr/
  platforms/
    linux-x64/
      build/
        onnxruntime/  # shallow clone and build here
        ffmpeg/
        speedreader/  # working directory for speedreader build
      bin/
        static/
          speedreader
        dynamic/  # TODO: later
          speedreader
          onnxruntime.so
      lib/
        onnxruntime/
          version.txt  # contains the version that was successfully built
          static/
          shared/
          include/
        ffmpeg/
          version.txt  # contains the version that was successfully built
          static/
          shared/
          include/
        speedreader_ort/
          static/
            speedreader_ort.a
          shared/
            speedreader_ort.so
        speedreader/  # speedreader will also be published as a lib used for,
                      # among other things, the python package
                      # no version, we always want the most recently built version here
          static/
          shared/
          include/
      packages/
        python/
        java/  # eventually, python is far and away #1 priority
        csharp/
