using System;
using DotnetMakeDeb;

namespace DotnetMakeDeb
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			return new MakeDeb().Execute(args);
		}
	}
}
