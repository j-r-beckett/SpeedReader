FROM ubuntu:20.04 AS build

# Set timezone and install basic dependencies
ENV TZ=America/Los_Angeles
RUN apt-get update && apt-get install -y tzdata && \
    ln -fs /usr/share/zoneinfo/America/Los_Angeles /etc/localtime && \
    dpkg-reconfigure -f noninteractive tzdata && \
    apt-get install -y \
    wget \
    git \
    build-essential \
    cmake \
    libopencv-dev \
    python3-pip \
    python3-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN pip3 install --upgrade pip && \
    pip3 install --upgrade setuptools wheel && \
    pip3 install six

# Using the CPU-only version to avoid CUDA conflicts
RUN pip3 install torch==2.0.0+cpu torchvision==0.15.0+cpu -f https://download.pytorch.org/whl/torch_stable.html

RUN pip3 install mmengine==0.7.4 && \
    pip3 install mmcv==2.0.1 && \
    pip3 install onnxruntime==1.15.0

RUN git clone https://github.com/open-mmlab/mmdeploy && \
    cd mmdeploy && \
    git checkout v1.3.1

RUN git clone https://github.com/open-mmlab/mmocr && \
    cd mmocr && \
    git checkout v1.0.1

RUN pip3 install mmdet==3.0.0
RUN pip3 install mmdeploy==1.3.1 && \
    pip3 install mmdet==3.0.0 && \
    pip3 install mmocr==1.0.1

# DBNet
RUN python3 mmdeploy/tools/deploy.py \
    mmdeploy/configs/mmocr/text-detection/text-detection_onnxruntime_dynamic.py \
    mmocr/configs/textdet/dbnet/dbnet_resnet18_fpnc_1200e_icdar2015.py \
    https://download.openmmlab.com/mmocr/textdet/dbnet/dbnet_resnet18_fpnc_1200e_icdar2015/dbnet_resnet18_fpnc_1200e_icdar2015_20220825_221614-7c0e94f2.pth \
    mmocr/demo/demo_text_det.jpg \
    --work-dir /models/dbnet_resnet18_fpnc_1200e_icdar2015 \
    --log-level   INFO \
    --dump-info

# Robust Scanner
RUN python3 mmdeploy/tools/deploy.py \
    mmdeploy/configs/mmocr/text-recognition/text-recognition_onnxruntime_dynamic.py \
    mmocr/configs/textrecog/robust_scanner/robustscanner_resnet31_5e_st-sub_mj-sub_sa_real.py \
    https://download.openmmlab.com/mmocr/textrecog/robust_scanner/robustscanner_resnet31_5e_st-sub_mj-sub_sa_real/robustscanner_resnet31_5e_st-sub_mj-sub_sa_real_20220915_152447-7fc35929.pth \
    mmocr/demo/demo_text_ocr.jpg \
    --work-dir /models/robustscanner_resnet31_5e_st-sub_mj-sub_sa_real \
    --log-level   INFO \
    --dump-info

COPY *.py .
RUN python3 /replace_padToWidth_with_pad.py /models/robustscanner_resnet31_5e_st-sub_mj-sub_sa_real/pipeline.json
RUN python3 /remove_dictionary_post_processing.py /models/robustscanner_resnet31_5e_st-sub_mj-sub_sa_real/pipeline.json

ENTRYPOINT ["/bin/bash"]
