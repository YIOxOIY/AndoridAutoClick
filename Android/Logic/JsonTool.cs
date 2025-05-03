using Newtonsoft.Json;
using System.IO;

namespace Android.Logic
{ 
	public class JsonTool
	{
		private static readonly string SaveFolder = "SaveData";
		/// <summary>
		/// 转换为json后并保存到本地
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filePath"></param>
		public static void SaveTxtFile<T>(T t,string filePath)
		{
			string _path = Environment.CurrentDirectory + "/" + SaveFolder+"/"+ filePath;
			string str = JsonConvert.SerializeObject(t);
			CheckDirectory();
			File.WriteAllText(_path, str);
		}
		static void CheckDirectory()
		{
			string _path = Environment.CurrentDirectory + "/" + SaveFolder;
			if (!Directory.Exists(_path))
			{
				Directory.CreateDirectory(_path);
			}
		}
		/// <summary>
		/// 加载文本数据 不含安全验证
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public static T LoadTxtFile<T>(string filePath) where T :new()
		{
			if (!File.Exists(filePath))
			{
				return new T();
			}
			string json = File.ReadAllText(filePath);
			return JsonConvert.DeserializeObject<T>(json);
		}
	}
}