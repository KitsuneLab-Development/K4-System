using CounterStrikeSharp.API.Core;
using K4System.Models;

namespace K4System;

public interface IModuleTime
{
	public void Initialize(bool hotReload);

	public void Release(bool hotReload);

	public void BeforeDisconnect(K4Player k4player);
}
