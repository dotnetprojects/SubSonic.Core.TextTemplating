using System;
using System.Collections.Generic;
using System.Text;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	public interface ITransformationRunFactoryService
	{
		IProcessTransformationRunFactory GetTransformationRunFactory (Guid ID);
	}
}
