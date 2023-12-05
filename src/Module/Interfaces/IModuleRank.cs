namespace K4System
{
	public interface IModuleRank
	{
		public void Initialize(bool hotReload);

		public void Release(bool hotReload);
	}
}