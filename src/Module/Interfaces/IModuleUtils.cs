namespace K4System
{
	public interface IModuleUtils
	{
		public void Initialize(bool hotReload);

		public void Release(bool hotReload);
	}
}