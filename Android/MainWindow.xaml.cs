using System;
using System.Diagnostics;
using System.Windows;
using Android;
using Android.Logic;
using System.Management;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using System.Windows.Input;
using System.Windows.Media;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Receivers;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Reflection;
using Newtonsoft.Json;

namespace ScreenMirror
{
	public partial class MainWindow : Window
	{
		private Process _adbProcess;
		private H264Decoder _decoder;
		static AdbClient adbClient;
		static DeviceData deviceData;
		public static MainWindow Instance;
		// 新增成员变量
		private DispatcherTimer _longPressTimer;
		private bool _isLongPress;
		private Point _dragStartPosition;
		OperationPlayer operationPlayer;
		public MainWindow()
		{
			InitializeComponent();
			Instance = this;
			var jsonSettings = new JsonSerializerSettings();
			jsonSettings.Converters.Add(new Vector2Converter());
			JsonConvert.DefaultSettings = () => jsonSettings;
			record.Style = (Style)Application.Current.Resources["NoHoverButtonStyle"];
			play.Style = (Style)Application.Current.Resources["NoHoverButtonStyle"];
			file.Style = (Style)Application.Current.Resources["NoHoverButtonStyle"];
			stop.Style = (Style)Application.Current.Resources["NoHoverButtonStyle"];
			play.Visibility = Visibility.Hidden;
			stop.Visibility = Visibility.Hidden;
			RefreshPlayState();
			InitializeFFmpeg();
			Start();
			_longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
			_longPressTimer.Tick += OnLongPressTriggered;
			points = new List<Point>();
			GetDeviceResolution();
			StartMirroring();
		}

		#region 初始化ADB、FFmpeg
		private void InitializeFFmpeg()
		{
			// 设置 FFmpeg 动态库路径
			FFmpegBinariesHelper.RegisterFFmpegBinaries();
			_decoder = new H264Decoder();
			_decoder.FrameDecoded += frame =>
				Dispatcher.Invoke(() => screenImage.Source = frame);
		}

		private void StartAdbStream()
		{

			_adbProcess = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "adb",
					Arguments = "exec-out screenrecord --bit-rate=4M --size 720x1280 --output-format=h264 -",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			//_adbProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8; // 避免二进制流编码错误
			_adbProcess.Start();
			// 等待进程初始化完成
		}
		void Log(string _value)
		{
			Dispatcher.Invoke(() => tip.Content = _value);
		}
		private void Start()
		{
			// 监控设备连接状态变化
			//var monitor = new DeviceMonitor(new AdbSocket());
			//monitor.DeviceConnected += (sender, e) => { Dispatcher.Invoke(() => tip.Content = $"设备已连接: {e.Device.Name}"); };
			//monitor.Start();
			if (!AdbServer.Instance.GetStatus().IsRunning)
			{
				AdbServer server = new AdbServer();
				StartServerResult result = server.StartServer(@"adb.exe", false);
				if (result != StartServerResult.Started)
				{
					Log("Can't start adb server");
				}
			}
			Log("Over");
			adbClient = new AdbClient();
			adbClient.Connect("127.0.0.1:62001");
			deviceData = adbClient.GetDevices().FirstOrDefault(); // Get first connected device

		}
		private async void StartMirroring()
		{
			try
			{
				StartAdbStream();
				await ProcessVideoStreamAsync();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error: {ex.Message}");
			}
		}

		private async Task ProcessVideoStreamAsync()
		{
			await Task.Run(() =>
			{
				using (var stream = _adbProcess.StandardOutput.BaseStream)
				{
					byte[] buffer = new byte[1024 * 1024];
					while (!_adbProcess.HasExited)
					{
						int bytesRead = stream.Read(buffer, 0, buffer.Length);
						if (bytesRead > 0)
							_decoder.DecodeFrame(buffer, bytesRead);
					}
				}
			});
		}
		#endregion
		private (int width, int height) _deviceResolution;

		// 获取设备分辨率
		private void GetDeviceResolution()
		{
			var receiver = new ConsoleOutputReceiver();
			adbClient.ExecuteRemoteCommand("wm size", deviceData, receiver);
			string _str = receiver.ToString().Replace("Physical size: ", "");
			_str = _str.Replace("\n","");
			_str = _str.Replace("\t","");
			var parts = _str.Split('x', StringSplitOptions.RemoveEmptyEntries);
			_deviceResolution = (int.Parse(parts[0]), int.Parse(parts[1]));
			Dispatcher.Invoke(()=> { InitRadio(_deviceResolution.width, _deviceResolution.height); });
		}
		public void InitRadio(double _x,double _y)
		{
			double _ratio = _x / _y;
			Log($"x:{_x} y:{_y} {_ratio.ToString()}   {(int)(height * _ratio)}  {(540 - ((double)height * _ratio)) / 2}");
			offset = (540 - ((double)height * _ratio)) / 2;
			width = (int)(height * _ratio);
		}
		double offset = 0;
		int height = 980;
		int width = 540;
		List<Point> points;
		#region 操作事件处理
		// 修改Image_MouseLeftButtonDown
		private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_dragStartPosition = e.GetPosition(screenImage);
			_longPressTimer.Start();
			points.Clear();
			Point _temp = e.GetPosition((IInputElement)sender);
			if (_temp.X < offset || _temp.X > screenImage.ActualWidth - offset)
			{
				return;
			}
			points.Add(_temp);
		}

		// 长按触发逻辑
		private void OnLongPressTriggered(object sender, EventArgs e)
		{
			_longPressTimer.Stop();
			
			// 发送长按开始命令（adb shell input swipe x y x y 2000）
			if (_longPressTimer.Interval.TotalMilliseconds > 400)
			{
				_isLongPress = true;
			}
		}

		// 修改Image_MouseLeftButtonUp
		private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			_longPressTimer.Stop();
			if (_isLongPress)
			{
				_isLongPress = false;
				Point _temp = e.GetPosition((IInputElement)sender);
				if (_temp.X < offset || _temp.X > screenImage.ActualWidth - offset)
				{
					points.Clear();
					return;
				}
				points.Add(_temp);
				Drag(sender,e);    // 发送拖拽结束命令
			}
			else
			{
				// 原有点击逻辑
				HandleNormalClick(sender,e);
			}
		}

		private void Drag(object sender, MouseButtonEventArgs e)
		{
			try
			{
				if (points.Count < 2) return;
				Point _start = ConvertToDevicePoint(points[0]);
				Point _end = ConvertToDevicePoint(points[1]);
				ExecuteDeviceSwip((int)_start.X, (int)_start.Y, (int)_end.X, (int)_end.Y);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		// 鼠标点击事件处理
		private void HandleNormalClick(object sender, MouseButtonEventArgs e)
		{
			try
			{
				Point _temp = e.GetPosition((IInputElement)sender);
				if (_temp.X < offset || _temp.X > screenImage.ActualWidth - offset)
				{
					return;
				}
				//_temp.X -= offset;
				FrameRecorder.AddData(new Vector2() { X = (int)_temp.X,Y = (int)_temp.Y});
				// 转换为设备坐标
				var (deviceX, deviceY) = ConvertToDeviceCoordinates(_temp);
				// 执行设备点击
				ExecuteDeviceClick(deviceX, deviceY);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}



		// 使用异步命令执行
		async void ExecuteAdbSwipe(string command)
		{
			await Task.Run(() => adbClient.ExecuteRemoteCommand(command, deviceData));
		}

		// 设备坐标转换
		private (int x, int y) ConvertToDeviceCoordinates(Point screenPoint)
		{
			// 获取实际显示尺寸
			var displayWidth = screenImage.ActualWidth - offset * 2;
			var displayHeight = screenImage.ActualHeight;

			// 计算缩放比例
			double scaleX = _deviceResolution.width / displayWidth;
			double scaleY = _deviceResolution.height / displayHeight;

			return (
				(int)((screenPoint.X-offset) * scaleX),
				(int)(screenPoint.Y * scaleY)
			);
		}

		private (int x, int y) ConvertToDeviceCoordinates(int _x,int _y)
		{
			// 获取实际显示尺寸
			var displayWidth = screenImage.ActualWidth - offset * 2;
			var displayHeight = screenImage.ActualHeight;

			// 计算缩放比例
			double scaleX = _deviceResolution.width / displayWidth;
			double scaleY = _deviceResolution.height / displayHeight;

			return (
				(int)((_x - offset) * scaleX),
				(int)(_y * scaleY)
			);
		}

		private Point ConvertToDevicePoint(Point screenPoint)
		{
			// 获取实际显示尺寸
			var displayWidth = screenImage.ActualWidth - offset * 2;
			var displayHeight = screenImage.ActualHeight;

			// 计算缩放比例
			double scaleX = _deviceResolution.width / displayWidth;
			double scaleY = _deviceResolution.height / displayHeight;

			return new Point (
				(int)((screenPoint.X - offset) * scaleX),
				(int)(screenPoint.Y * scaleY)
			);
		}
		// 执行ADB点击命令
		private void ExecuteDeviceClick(int x, int y)
		{
			Log($"X:{x } offset {offset} {x+(int)offset}   ____ Y:{y}");
			adbClient.ExecuteRemoteCommand($"input tap {x} {y}", deviceData);
		}

		/// <summary>
		/// 根据数据播放
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void Replay(int x,int y)
		{
			// 转换为设备坐标
			var (deviceX, deviceY) = ConvertToDeviceCoordinates(x,y);
			// 执行设备点击
			ExecuteDeviceClick(deviceX, deviceY);
		}

		/// <summary>
		/// 执行ADB 拖拽命令
		/// </summary>
		/// <param name="_startX"></param>
		/// <param name="_startY"></param>
		/// <param name="_endX"></param>
		/// <param name="_endY"></param>
		private void ExecuteDeviceSwip(int _startX,int _startY,int _endX,int _endY)
		{
			string command = $"input swipe {_startX} {_startY} {_endX} {_endY} 50";
			Log(command);
			adbClient.ExecuteRemoteCommand(command,deviceData);
		}
		#endregion


		#region 按钮点击
		private bool isStart = false; // 状态标识

		private void ActionButton_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			var brush = button.Background as ImageBrush;

			
			isStart = !isStart;
			// 切换图片路径
			string newPath = isStart ? "/Resources/stop.png" : "/Resources/start.png";

			// 使用Pack URI确保路径正确[6](@ref)
			brush.ImageSource = new BitmapImage(
				new Uri("pack://application:,,," + newPath, UriKind.Absolute));

			if (isStart)
			{
				StartRecord();
			}
			else
			{
				StopRecord();
			}
		}

		

		FileSelectorWindow selector;
		// 主窗口按钮点击事件
		private void File_Click(object sender, RoutedEventArgs e)
		{
			selector = new FileSelectorWindow();
			selector.Owner = this;
			selector.ShowDialog();
		}
		#endregion

		#region 录制操作
		public void StartRecord()
		{
			isStart = true;
			FrameRecorder.StartRecording();
		}

		public void StopRecord()
		{
			isStart = false;
			FrameRecorder.StopRecording();
			selector?.Refresh();
		}

		public void Replay(OperationRecord _data)
		{
			if (operationPlayer == null)
			{
				operationPlayer = new OperationPlayer();
				operationPlayer.window = this;
			}
			operationPlayer.Play(_data);
			_isPlaying = true;
			RefreshPlayState();
			play.Visibility = Visibility.Visible;
			stop.Visibility = Visibility.Visible;
		}
		#endregion

		#region 播放操作
		bool _isPlaying;
		/// <summary>
		/// 刷新播放状态
		/// </summary>
		void RefreshPlayState()
		{
			var brush = play.Background as ImageBrush;

			// 切换图片路径
			string newPath = _isPlaying ? "/Resources/pause.png" : "/Resources/play.png";

			// 使用Pack URI确保路径正确[6](@ref)
			brush.ImageSource = new BitmapImage(new Uri("pack://application:,,," + newPath, UriKind.Absolute));
		}

		private void Stop_Click(object sender, RoutedEventArgs e)
		{
			StopPlay();
		}

		private void Play_Click(object sender, RoutedEventArgs e)
		{
			_isPlaying = !_isPlaying;
			RefreshPlayState();
			if (_isPlaying)
			{
				operationPlayer.Play();
			}
			else
			{
				operationPlayer.Pause();
			}
		}

		public void StopPlay()
		{
			Dispatcher.Invoke(Stop_Play);
		}

		void Stop_Play()
		{
			_isPlaying = false;
			play.Visibility = Visibility.Hidden;
			stop.Visibility = Visibility.Hidden;
			operationPlayer.Stop();
			RefreshPlayState();
		}
		#endregion

		protected override void OnClosed(EventArgs e)
		{
			_adbProcess?.Kill();
			_decoder?.Dispose();
			base.OnClosed(e);
		}
	}
}