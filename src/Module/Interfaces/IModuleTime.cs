namespace K4System
{
	public interface IModuleTime
	{
		public void Initialize(bool hotReload);

		public void Release(bool hotReload);
	}
}