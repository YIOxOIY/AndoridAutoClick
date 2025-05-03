using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Android.Logic
{

	// 解码器实现
	public unsafe class H264Decoder : IDisposable
	{
		private AVCodecContext* _codecContext;
		private AVFrame* _frame;
		private SwsContext* _swsContext;

		public H264Decoder()
		{
			// 查找 H.264 解码器
			AVCodec* codec = ffmpeg.avcodec_find_decoder_by_name("h264_cuvid");
			if (codec == null)
			{
				// 如果 avcodec_find_decoder 失败，尝试使用 avcodec_find_decoder_by_name
				codec = ffmpeg.avcodec_find_decoder_by_name("h264");
			}

			_codecContext = ffmpeg.avcodec_alloc_context3(codec);
			ffmpeg.avcodec_open2(_codecContext, codec, null);
			_frame = ffmpeg.av_frame_alloc();
		}

		public void DecodeFrame(byte[] data, int length)
		{
			fixed (byte* ptr = data)
			{
				var packet = new AVPacket { data = ptr, size = length };
				ffmpeg.avcodec_send_packet(_codecContext, &packet);

				while (!isDisable)
				{
					try
					{
						int ret = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
						if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF) { break; }
						ConvertFrameToBitmap();
					}
					catch
					{

					}
				}
			}
		}

		bool isDisable;
		public void ConvertFrameToBitmap()
		{
			// 强制宽高为偶数（解决奇偶对齐问题）[1,3](@ref)
			int width = _frame->width & ~1;
			int height = _frame->height & ~1;

			// 初始化像素格式转换上下文（动态适配分辨率变化）
			if (_swsContext == null || _frame->width != width || _frame->height != height)
			{
				_swsContext = ffmpeg.sws_getContext(
					_frame->width, _frame->height,
					(AVPixelFormat)_frame->format,    // 源格式（如YUV420P）
					width, height,                    // 目标分辨率（强制偶数）
					AVPixelFormat.AV_PIX_FMT_RGB24,   // 目标格式（与WPF兼容）
					ffmpeg.SWS_BILINEAR, null, null, null);
			}

			// 计算目标缓冲区大小（考虑行对齐）
			int dstStride = width * 3;  // RGB24每像素3字节
			byte[] buffer = new byte[dstStride * height];

			fixed (byte* dstPtr = buffer)
			{
				byte*[] dstData = { dstPtr };
				int[] dstLinesize = { dstStride };

				// 执行像素格式转换
				ffmpeg.sws_scale(_swsContext,
					_frame->data, _frame->linesize, 0, _frame->height,
					dstData, dstLinesize);

				// 创建WPF兼容的BitmapSource（自动处理DPI和颜色空间）
				var bitmap = BitmapSource.Create(
					width, height,
					96, 96,  // 标准屏幕DPI
					PixelFormats.Rgb24,
					null,
					buffer,
					dstStride);  // 关键：必须与转换后的stride一致

				bitmap.Freeze(); // 多线程安全
				FrameDecoded?.Invoke(bitmap);
			}

			ffmpeg.av_frame_unref(_frame);
		}

		public void Dispose()
		{
			isDisable = true;
			// 修正指针传递方式
			if (_frame != null) av_frame_free(ref _frame);
			if (_codecContext != null) avcodec_free_context(ref _codecContext);
			if (_swsContext != null) ffmpeg.sws_freeContext(_swsContext);
		}

		public unsafe static void av_frame_free(ref AVFrame* frame)
		{
			if (frame != null)
			{
				// 释放数据缓冲区引用
				ffmpeg.av_frame_unref(frame);

				// 释放结构体并置空指针
				AVFrame* localPtr = frame;
				ffmpeg.av_frame_free(&localPtr);
				frame = localPtr; // 关键：通过ref将置空后的指针传回
			}
		}

		public unsafe static void avcodec_free_context(ref AVCodecContext* avctx)
		{
			if (avctx != null)
			{
				// 通过局部变量操作指针地址
				AVCodecContext* localPtr = avctx;
				ffmpeg.avcodec_free_context(&localPtr);
				avctx = localPtr; // 通过ref回传置空后的指针[6,7](@ref)
			}
		}
		public event Action<BitmapSource> FrameDecoded;


	}
}