# ============================================================================
# 基于 Flask + YOLO 的视频目标检测后端服务
# 功能：视频上传、实时目标检测、结果存储、视频流推送
# 技术栈：Flask + MySQL + OpenCV + YOLOv11
# ============================================================================

import mysql.connector
from flask import Flask, jsonify, request, Response
import os
import cv2
import threading
import time
from flask_cors import CORS
from ultralytics import YOLO
from datetime import datetime
from werkzeug.utils import send_file, secure_filename

# 初始化 Flask 应用
app = Flask(__name__)
CORS(app)  # 启用跨域请求支持，允许前端 MAUI 应用访问

# 配置上传文件夹
UPLOAD_FOLDER = 'uploads'
os.makedirs(UPLOAD_FOLDER, exist_ok=True)  # 确保上传目录存在
app.config['UPLOAD_FOLDER'] = UPLOAD_FOLDER

# 加载 YOLO 模型
model = YOLO('yolo11n.pt')


# ============================================================================
# 数据库操作辅助函数
# ============================================================================

def get_connection():
    """
    获取 MySQL 数据库连接
    返回：连接对象，失败时返回 None
    """
    config = {
        'host': 'localhost',
        'user': 'root',
        'password': '123456',
        'database': 'object_detection',
    }
    try:
        connection = mysql.connector.connect(**config)
        return connection
    except Exception as e:
        print(f'Error: 连接失败 {e}')
        return None


def save_videos_info(filename, filepath):
    """
    保存视频信息到数据库
    参数：
        filename: 视频文件名
        filepath: 视频文件路径
    返回：新插入的视频 ID，失败时返回 None
    """
    connection = get_connection()
    if not connection:
        return None
    try:
        cursor = connection.cursor()
        sql = 'INSERT INTO videos(filename, filepath, status) VALUES(%s, %s, %s)'
        cursor.execute(sql, (filename, filepath, 'pending'))
        video_id = cursor.lastrowid  # 获取自增 ID
        connection.commit()
        return video_id
    finally:
        cursor.close()
        connection.close()


def update_video_status(video_id, status):
    """
    更新视频处理状态
    参数：
        video_id: 视频 ID
        status: 新状态（pending/processing/completed/failed）
    """
    connection = get_connection()
    if not connection:
        return None
    try:
        cursor = connection.cursor()
        sql = 'UPDATE videos SET status = %s WHERE id = %s'
        cursor.execute(sql, (status, video_id))
        connection.commit()
    finally:
        cursor.close()
        connection.close()


def save_detection_results(video_id, results):
    """
    批量保存检测结果到数据库
    参数：
        video_id: 视频 ID
        results: 检测结果列表，每个结果包含 class, confidence, bbox, frame
    """
    connection = get_connection()
    if not connection:
        return
    try:
        cursor = connection.cursor()
        sql = '''INSERT INTO detection_results 
                 (video_id, object_class, confidence, bbox_x1, bbox_y1, bbox_x2, bbox_y2, frame_number) 
                 VALUES (%s, %s, %s, %s, %s, %s, %s, %s)'''

        for result in results:
            cursor.execute(sql, (
                video_id,
                result['class'],
                result['confidence'],
                result['bbox'][0],  # x1
                result['bbox'][1],  # y1
                result['bbox'][2],  # x2
                result['bbox'][3],  # y2
                result['frame']
            ))
        connection.commit()
    finally:
        cursor.close()
        connection.close()


# ============================================================================
# 视频处理函数（传统批处理模式）
# ============================================================================

def process_video(video_path, video_id):
    """
    批处理模式：处理整个视频文件
    特点：每 10 帧检测一次，适合离线处理
    参数：
        video_path: 视频文件路径
        video_id: 视频 ID
    """
    try:
        update_video_status(video_id, 'processing')
        cap = cv2.VideoCapture(video_path)
        results_list = []
        frame_count = 0

        while True:
            ret, frame = cap.read()
            if not ret:
                break

            # 每 10 帧检测一次，提高处理速度
            if frame_count % 10 == 0:
                detections = model(frame, verbose=False, device='cpu')[0]

                for box in detections.boxes:
                    class_id = int(box.cls[0].item())
                    object_class = model.names[class_id]
                    confidence = box.conf[0].item()
                    bbox = box.xyxy[0].tolist()

                    results_list.append({
                        'frame': frame_count,
                        'class': object_class,
                        'confidence': confidence,
                        'bbox': bbox
                    })

            frame_count += 1

        cap.release()
        save_detection_results(video_id, results_list)
        update_video_status(video_id, 'completed')
        print(f"视频 {video_id} 处理完成，共处理 {len(results_list)} 个检测结果")

    except Exception as e:
        print(f"处理视频时出错：{e}")
        update_video_status(video_id, 'failed')


# ============================================================================
# API 响应辅助函数
# ============================================================================

def error_response(message):
    """返回错误响应"""
    return jsonify({'error': message}), 400


def success_response(data=None):
    """返回成功响应"""
    return jsonify({'message': 'success', 'data': data}), 200


# ============================================================================
# 视频管理 API
# ============================================================================

@app.route('/api/videos', methods=['GET'])
def get_videos():
    """获取所有视频列表（按 ID 降序）"""
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT * FROM videos ORDER BY id DESC')
        videos = cursor.fetchall()
        return success_response(videos)
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


@app.route('/api/videos/<int:video_id>/results', methods=['GET'])
def get_video_results(video_id):
    """获取指定视频的所有检测结果"""
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT * FROM detection_results WHERE video_id = %s', (video_id,))
        results = cursor.fetchall()
        return success_response(results)
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


# ============================================================================
# 视频上传 API（批处理模式）
# ============================================================================

@app.route('/api/upload', methods=['POST'])
def upload_videos():
    """
    上传视频并启动批处理
    流程：接收文件 → 保存文件 → 记录数据库 → 异步处理
    """
    if 'file' not in request.files:
        return error_response('No file')

    file = request.files['file']
    if file.filename == '':
        return error_response('No filename')

    # 保存文件
    filepath = os.path.join(app.config['UPLOAD_FOLDER'], file.filename)
    file.save(filepath)

    # 保存视频信息到数据库
    video_id = save_videos_info(file.filename, filepath)
    if not video_id:
        return error_response('保存视频信息失败')

    # 异步处理视频（不阻塞响应）
    thread = threading.Thread(target=process_video, args=(filepath, video_id))
    thread.start()

    return success_response({
        'video_id': video_id,
        'filename': file.filename,
        'message': '视频已上传，正在处理中'
    })


@app.route('/api/videos/<int:video_id>', methods=['GET'])
def get_video_detail(video_id):
    """获取单个视频的详细信息"""
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT * FROM videos WHERE id = %s', (video_id,))
        video = cursor.fetchone()

        if not video:
            return error_response('视频不存在')

        return success_response(video)
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


@app.route('/health', methods=['GET'])
def health():
    """健康检查接口"""
    return jsonify({'status': 'healthy'}), 200


# ============================================================================
# VideoProcessor 类：实时视频处理器
# 核心功能：跳帧处理、实时 FPS 计算、检测结果缓存
# ============================================================================

# 存储所有活跃的处理任务
processing_tasks = {}


class VideoProcessor:
    """
    实时视频处理器类
    特性：
    - 支持跳帧处理（每 2 帧检测一次）
    - 实时计算处理速度（FPS）
    - 缓存检测结果用于跳过的帧
    - 异步线程处理
    """

    def __init__(self, video_path, video_id, model):
        """
        初始化处理器
        参数：
            video_path: 视频文件路径
            video_id: 视频 ID
            model: YOLO 模型实例
        """
        self.video_path = video_path
        self.video_id = video_id
        self.model = model
        self.cap = None
        self.fps = 0  # 实际处理速度（帧/秒）
        self.frame_count = 0  # 已处理帧数
        self.total_frames = 0  # 视频总帧数
        self.processing = False  # 处理状态标志
        self.results = []  # 检测结果列表
        self.output_path = None  # 处理后视频保存路径
        self.video_info = {}  # 视频元信息
        self.last_frame_time = time.time()  # 上一帧处理时间
        self.frame_processing_times = []  # 每帧处理时间记录
        self.process_every_n_frames = 2  # 跳帧间隔（每 2 帧处理 1 次）
        self.last_detections = []  # 上一次检测结果（用于跳过的帧）

    def get_video_info(self):
        """
        读取视频文件的基本信息
        返回：包含 fps、帧数、分辨率、时长的字典
        """
        if not os.path.exists(self.video_path):
            return None

        cap = cv2.VideoCapture(self.video_path)
        fps = cap.get(cv2.CAP_PROP_FPS)
        frame_count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
        width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        duration = frame_count / fps if fps > 0 else 0

        cap.release()

        return {
            'fps': fps,
            'frame_count': frame_count,
            'width': width,
            'height': height,
            'duration': duration,
            'resolution': f"{width}x{height}"
        }

    def process_frame(self, frame, use_last_detections=False):
        """
        处理单帧图像
        参数：
            frame: 输入帧
            use_last_detections: 是否复用上一次检测结果（用于跳过的帧）
        返回：处理后的帧和检测结果
        """
        if use_last_detections and self.last_detections:
            # 跳过推理，直接使用缓存的检测结果
            detections = self.last_detections
        else:
            # 执行 YOLO 推理（输入尺寸 640，平衡精度和速度）
            results = self.model(frame, verbose=False, device='cpu', imgsz=640)[0]
            detections = []

            for box in results.boxes:
                class_id = int(box.cls[0].item())
                object_class = self.model.names[class_id]
                confidence = box.conf[0].item()
                bbox = box.xyxy[0].tolist()

                # 只保留置信度>=0.5 的检测结果
                if confidence >= 0.5:
                    detections.append({
                        'class': object_class,
                        'confidence': confidence,
                        'bbox': bbox,
                        'frame': self.frame_count
                    })

            self.last_detections = detections  # 缓存检测结果

        # 在帧上绘制检测框
        for detection in detections:
            x1, y1, x2, y2 = map(int, detection['bbox'])
            cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 1)  # 绿色框，线宽 1
            label = f"{detection['class']}: {detection['confidence']:.2f}"
            cv2.putText(frame, label, (x1, y1 - 5),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 255, 0), 1)  # 字体大小 0.4

        return frame, detections

    def process_realtime(self):
        """
        实时处理视频的主流程
        流程：获取视频信息 → 创建输出文件 → 逐帧处理 → 保存结果
        """
        self.processing = True

        # 获取视频信息
        self.video_info = self.get_video_info()
        if self.video_info:
            self.total_frames = self.video_info['frame_count']

            # 更新数据库中的视频元信息
            connection = get_connection()
            if connection:
                try:
                    cursor = connection.cursor()
                    sql = """
                        UPDATE videos 
                        SET duration = %s, resolution = %s, fps = %s,
                            process_started = CURRENT_TIMESTAMP 
                        WHERE id = %s
                    """
                    cursor.execute(sql, (
                        self.video_info['duration'],
                        self.video_info['resolution'],
                        self.video_info['fps'],
                        self.video_id
                    ))
                    connection.commit()
                finally:
                    cursor.close()
                    connection.close()

        # 创建输出目录和文件路径
        output_dir = os.path.join(app.config['UPLOAD_FOLDER'], 'processed')
        os.makedirs(output_dir, exist_ok=True)
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        self.output_path = os.path.join(output_dir, f"processed_{self.video_id}_{timestamp}.mp4")

        # 初始化视频读取器和写入器
        cap = cv2.VideoCapture(self.video_path)
        width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        fourcc = cv2.VideoWriter_fourcc(*'mp4v')
        video_fps = self.video_info['fps'] if self.video_info and 'fps' in self.video_info else 30
        out = cv2.VideoWriter(self.output_path, fourcc, video_fps, (width, height))

        self.results = []
        self.frame_count = 0
        self.last_frame_time = time.time()  # 重置计时器

        # 主处理循环
        while cap.isOpened() and self.processing:
            ret, frame = cap.read()
            if not ret:
                break

            # 判断是否需要处理这一帧（跳帧策略）
            should_process = (self.frame_count % self.process_every_n_frames == 0)

            # 处理帧
            processed_frame, detections = self.process_frame(frame, use_last_detections=not should_process)

            # 只保存实际处理帧的检测结果
            if should_process:
                self.results.extend(detections)

            # 写入处理后的帧到输出文件
            out.write(processed_frame)

            self.frame_count += 1

            # 计算实时处理速度（FPS）
            if self.frame_count > 1:
                total_time = time.time() - self.last_frame_time
                self.fps = self.frame_count / total_time

        cap.release()
        out.release()

        # 批量保存检测结果到数据库
        if self.results:
            save_detection_results(self.video_id, self.results)

        # 更新数据库状态
        connection = get_connection()
        if connection:
            try:
                cursor = connection.cursor()
                sql = """
                    UPDATE videos 
                    SET status = 'completed', 
                        processed_path = %s,
                        process_completed = CURRENT_TIMESTAMP
                    WHERE id = %s
                """
                cursor.execute(sql, (self.output_path, self.video_id))
                connection.commit()
            finally:
                cursor.close()
                connection.close()

        self.processing = False
        return self.output_path

    def get_progress(self):
        """获取处理进度（百分比）"""
        if self.total_frames == 0:
            return 0
        return min(100, (self.frame_count / self.total_frames) * 100)


# ============================================================================
# 实时处理 API
# ============================================================================

@app.route('/api/videos/<int:video_id>/process_realtime', methods=['POST'])
def start_realtime_processing(video_id):
    """启动指定视频的实时处理任务"""
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT * FROM videos WHERE id = %s', (video_id,))
        video = cursor.fetchone()

        if not video:
            return error_response('视频不存在')

        if video['status'] == 'processing':
            return error_response('视频正在处理中')

        # 更新状态为处理中
        cursor.execute('UPDATE videos SET status = "processing" WHERE id = %s', (video_id,))
        connection.commit()

        # 创建处理任务
        processor = VideoProcessor(video['filepath'], video_id, model)
        processing_tasks[video_id] = processor

        # 在新线程中开始处理（不阻塞 API 响应）
        thread = threading.Thread(target=processor.process_realtime)
        thread.start()

        return success_response({
            'video_id': video_id,
            'message': '开始实时处理',
            'task_id': str(video_id)
        })
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


@app.route('/api/videos/<int:video_id>/process_status', methods=['GET'])
def get_processing_status(video_id):
    """
    获取视频处理状态
    返回：处理状态、进度、当前帧、总帧数、FPS 等信息
    """
    # 优先从内存中的处理任务获取状态
    if video_id in processing_tasks:
        processor = processing_tasks[video_id]
        return success_response({
            'processing': processor.processing,
            'progress': processor.get_progress(),
            'current_frame': processor.frame_count,
            'total_frames': processor.total_frames,
            'fps': processor.fps,  # 实时处理速度
            'results_count': len(processor.results),
            'status': 'processing' if processor.processing else 'completed'
        })

    # 从数据库获取状态（处理完成后）
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT status FROM videos WHERE id = %s', (video_id,))
        video = cursor.fetchone()

        if video:
            return success_response({
                'processing': video['status'] == 'processing',
                'progress': 100 if video['status'] == 'completed' else 0,
                'status': video['status'],
                'fps': video.get('fps', 0)
            })
        else:
            return error_response('视频不存在')
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


@app.route('/api/videos/<int:video_id>/processed_video', methods=['GET'])
def get_processed_video(video_id):
    """获取处理后视频的信息（路径和下载 URL）"""
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT processed_path FROM videos WHERE id = %s', (video_id,))
        video = cursor.fetchone()

        if video and video['processed_path'] and os.path.exists(video['processed_path']):
            return success_response({
                'processed_path': video['processed_path'],
                'url': f'/api/videos/{video_id}/processed_video_file'
            })
        else:
            return error_response('处理后的视频不存在')
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


@app.route('/api/videos/<int:video_id>/processed_video_file', methods=['GET'])
def download_processed_video(video_id):
    """下载处理后的视频文件"""
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT processed_path FROM videos WHERE id = %s', (video_id,))
        video = cursor.fetchone()

        if video and video['processed_path'] and os.path.exists(video['processed_path']):
            return send_file(video['processed_path'],
                             as_attachment=True,
                             download_name=f'processed_{video_id}.mp4')
        else:
            return error_response('视频文件不存在')
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


# ============================================================================
# 视频上传 + 实时处理一体化 API
# ============================================================================

@app.route('/api/upload_realtime', methods=['POST'])
def upload_and_process_realtime():
    """
    上传视频并立即开始实时处理
    流程：接收文件 → 保存文件 → 记录数据库 → 创建处理器 → 异步处理
    特点：保留原始文件名（支持中文），直接启动实时处理
    """
    if 'file' not in request.files:
        return error_response('No file')

    file = request.files['file']
    if file.filename == '':
        return error_response('No filename')

    # 保存文件 - 保留原始文件名，支持中文
    filename = file.filename
    filepath = os.path.join(app.config['UPLOAD_FOLDER'], filename)
    file.save(filepath)

    # 保存视频信息到数据库
    video_id = save_videos_info(filename, filepath)
    if not video_id:
        return error_response('保存视频信息失败')

    # 开始实时处理
    processor = VideoProcessor(filepath, video_id, model)
    processing_tasks[video_id] = processor

    # 在新线程中开始处理
    thread = threading.Thread(target=processor.process_realtime)
    thread.start()

    return success_response({
        'video_id': video_id,
        'filename': filename,
        'message': '视频已上传，开始实时处理',
        'redirect_url': f'/realtime/{video_id}'  # 前端跳转 URL
    })


# ============================================================================
# 视频流推送 API（MJPEG 格式）
# ============================================================================

@app.route('/api/videos/<int:video_id>/stream')
def video_stream(video_id):
    """
    实时视频流推送接口
    格式：MJPEG（Motion JPEG），适合 WebView 直接播放
    功能：逐帧检测目标并绘制检测框，实时推送到前端
    """
    connection = get_connection()
    if not connection:
        return error_response('数据库连接失败')

    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute('SELECT filepath FROM videos WHERE id = %s', (video_id,))
        video = cursor.fetchone()

        if not video:
            return error_response('视频不存在')

        def generate():
            """生成器函数：逐帧生成 JPEG 数据"""
            cap = cv2.VideoCapture(video['filepath'])
            model = YOLO('yolo11n.pt')  # 重新加载模型

            while cap.isOpened():
                ret, frame = cap.read()
                if not ret:
                    break

                # YOLO 目标检测
                results = model(frame, verbose=False, device='cpu')[0]

                # 绘制检测框
                for box in results.boxes:
                    class_id = int(box.cls[0].item())
                    object_class = model.names[class_id]
                    confidence = box.conf[0].item()
                    bbox = box.xyxy[0].tolist()

                    x1, y1, x2, y2 = map(int, bbox)
                    cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
                    label = f"{object_class}: {confidence:.2f}"
                    cv2.putText(frame, label, (x1, y1 - 10),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2)

                # 编码为 JPEG
                ret, jpeg = cv2.imencode('.jpg', frame)
                frame_bytes = jpeg.tobytes()

                # 生成 MJPEG 帧
                yield (b'--frame\r\n'
                       b'Content-Type: image/jpeg\r\n\r\n' + frame_bytes + b'\r\n')

                # 控制帧率（约 30fps）
                time.sleep(0.033)

            cap.release()

        # 返回 MJPEG 流响应
        return Response(generate(),
                        mimetype='multipart/x-mixed-replace; boundary=frame')
    except Exception as e:
        return error_response(str(e))
    finally:
        cursor.close()
        connection.close()


# ============================================================================
# 程序入口
# ============================================================================

if __name__ == '__main__':
    app.run(debug=True, port=5000, threaded=True)