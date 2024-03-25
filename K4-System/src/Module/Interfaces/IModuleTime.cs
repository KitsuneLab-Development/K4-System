using CounterStrikeSharp.API.Core;

namespace K4System;

public interface IModuleTime
{
	public void Initialize(bool hotReload);

	public void Release(bool hotReload);

	public void BeforeDisconnect(CCSPlayerController player);
}
