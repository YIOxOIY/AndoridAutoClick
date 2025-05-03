using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Android.Logic
{
	

	public static class FrameRecorder
	{
		private static OperationRecord _currentRecord;
		private static Timer _frameTimer;
		private static int _currentFrame = 0;
		private static bool _isRecording = false;
		private static readonly object _lock = new object();

		/// <summary>
		/// 开始录制（50ms间隔自动记录帧号）
		/// </summary>
		public static void StartRecording()
		{
			if (_isRecording) return;

			// 初始化录制对象
			_currentRecord = new OperationRecord();
			_currentRecord.data = new Dictionary<int, List<Vector2>>();
			_currentFrame = 0;
			_isRecording = true;

			// 定时器配置（50ms触发）
			_frameTimer = new Timer(50);
			_frameTimer.Elapsed += (sender, e) => IncrementFrame();
			_frameTimer.AutoReset = true;
			_frameTimer.Start();
		}

		/// <summary>
		/// 停止录制并返回数据
		/// </summary>
		public static OperationRecord StopRecording()
		{
			if (!_isRecording) return null;

			_frameTimer?.Stop();
			_frameTimer?.Dispose();
			_isRecording = false;
			JsonTool.SaveTxtFile(_currentRecord, DateTime.UtcNow.ToString("yyyy_M_d_HH_mm") + "_file.json");
			return _currentRecord;
		}

		/// <summary>
		/// 外部手动添加数据（如点击事件）
		/// </summary>
		public static void AddData(Vector2 point)
		{
			if (!_isRecording) return;

			lock (_lock)
			{
				if (!_currentRecord.data.ContainsKey(_currentFrame))
				{
					_currentRecord.data[_currentFrame] = new List<Vector2>();
				}
				_currentRecord.data[_currentFrame].Add(point);
			}
		}

		// 自动递增帧号（线程安全）
		private static void IncrementFrame()
		{
			Interlocked.Increment(ref _currentFrame);
		}
	}
}
