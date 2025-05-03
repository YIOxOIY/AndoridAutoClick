using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.ComponentModel;

namespace Android.Logic
{

	public struct Vector2
	{
		public int X;
		public int Y;
	}

	public class OperationRecord
	{
		[DefaultValue(typeof(Dictionary<int, List<Vector2>>), "new")]
		public Dictionary<int, List<Vector2>> data;
	}
}
