
using Android.Logic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;

namespace ScreenMirror
{
    /// <summary>
    /// FileSelectorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FileSelectorWindow : Window
    {
        public FileSelectorWindow()
        {
            InitializeComponent();
			Start();
        }

        void Start()
        {
			// 获取当前运行目录下的SaveData路径
			string saveDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SaveData");
			if (!Directory.Exists(saveDataPath))
			{
				Directory.CreateDirectory(saveDataPath);
			}
			// 筛选.bin文件
			var binFiles = Directory.EnumerateFiles(saveDataPath, "*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToList();
			// 清空已有项避免重复
			fileList.Items.Clear();

			// 动态添加列表项
			foreach (var fileName in binFiles)
			{
				var item = new ListViewItem();
				item.Content = fileName; // WPF需使用数据绑定
				item.Tag = Path.Combine(saveDataPath, fileName); // 存储完整路径
				fileList.Items.Add(item);
			}
			if (fileList.Items.Count > 0)
				fileList.SelectedIndex = 0;
		}

		private void SelectFile_Click(object sender, RoutedEventArgs e)
		{
			if (fileList.SelectedItem == null)
			{
				MessageBox.Show("请先选择文件", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			// 继续执行文件处理逻辑...
			if (fileList.Items.Count == 0)
			{
				MessageBox.Show("文件列表为空，请先录制文件");
				return;
			}
			ListViewItem _item = (ListViewItem)fileList.SelectedItem;
			OperationRecord record = JsonTool.LoadTxtFile<OperationRecord>((string)_item.Tag);
			(Owner as MainWindow)?.Replay(record);
		}

		/// <summary>
		/// 刷新
		/// </summary>
		public void Refresh()
		{
			fileList.Items.Refresh(); // 确保数据更新同步
		}

	}
}
