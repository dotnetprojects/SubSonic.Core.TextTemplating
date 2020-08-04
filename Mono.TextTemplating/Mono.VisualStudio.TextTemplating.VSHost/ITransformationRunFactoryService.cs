using System;
using System.Collections.Generic;
using System.Text;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	public interface ITransformationRunFactoryService
	{
		IProcessTransformationRunFactory GetTransformationRunFactory (Guid ID);
		/// <summary>
		/// Let the service now it can shutdown
		/// </summary>
		/// <returns></returns>
		bool Shutdown ();
	}
}
