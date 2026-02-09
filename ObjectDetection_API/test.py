import torch
print(torch.__version__) # 输出当前安装的PyTorch版本
print(torch.cuda.is_available())
print(torch.cuda.device_count())