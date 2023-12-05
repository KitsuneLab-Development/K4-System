namespace K4System
{
	public interface IModuleStat
	{
		public void Initialize(bool hotReload);

		public void Release(bool hotReload);
	}
}