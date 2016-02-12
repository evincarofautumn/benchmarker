﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Benchmarker.Models
{
	public class Machine
	{
		public string Name { get; set; }
		public int DefaultTimeout { get; set; } = 120;
		public Dictionary<string, int> BenchmarkTimeouts { get; set; }
		public List<string> ExcludeBenchmarks { get; set; }
		public string Architecture { get; set; }

		public Machine ()
		{
		}

		public static Machine LoadFromString (string content)
		{
			return JsonConvert.DeserializeObject<Machine> (content);
		}

		public IDictionary<string, string> ApiObject
		{
			get {
				var dict = new Dictionary<string, string> ();
				dict ["Name"] = Name;
				dict ["Architecture"] = Architecture;
				return dict;
			}
		}
	}
}
