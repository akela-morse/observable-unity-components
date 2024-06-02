using System;

namespace ObervableUnityComponents
{
	[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
	public sealed class WatchAttribute : Attribute
	{
		public WatchAttribute() { }
	}
}