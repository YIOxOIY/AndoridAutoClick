using ScreenMirror;
using System;
using System.Timers;
using System.Windows;
using Timer = System.Timers.Timer;

namespace Android.Logic
{
    public class OperationPlayer
    {
		private OperationRecord _currentRecord;
		private Timer _frameTimer;
		private int _currentFrame = 0;
		private readonly object _lock = new object();
		public MainWindow window;
		int max;
		public void Play(OperationRecord _data)
		{
			_currentRecord = _data;
			if (_currentRecord.data != null)
			{
				foreach (var item in _currentRecord.data.Keys)
				{
					max = Math.Max(max, item);
				}
			}
			_currentFrame = 0;
			_frameTimer?.Stop();
			_frameTimer?.Dispose();
			_frameTimer = new Timer(50);
			_frameTimer.Enabled = true;
			_frameTimer.Elapsed += IncrementFrame;
			_frameTimer.AutoReset = true;
			_frameTimer.Start();
		}

		// 自动递增帧号（线程安全）
		private void IncrementFrame(object sender,ElapsedEventArgs e)
		{
			Interlocked.Increment(ref _currentFrame);
			try
			{
				if (_currentRecord == null)
				{
					return;
				}
				if (_currentRecord.data == null)
				{
					return;
				}

				if (_currentRecord.data.ContainsKey(_currentFrame))
				{
					List<Vector2> _data = _currentRecord.data[_currentFrame];
					foreach (var item in _data)
					{
						window.Replay(item.X, item.Y);
					}
				}
				if (_currentFrame >= max)
				{
					Stop();
					MainWindow.Instance.StopPlay();
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}


		public void Pause()
		{
			if (_frameTimer == null) return;
			_frameTimer.Enabled = false;
		}

		public void Play()
		{
			if (_frameTimer == null) return;
			_frameTimer.Enabled = false;
		}

		public void Stop()
		{
			if (_frameTimer == null) return;
			_frameTimer.Elapsed -= IncrementFrame;
			_frameTimer.Stop();
		}
	}
}
