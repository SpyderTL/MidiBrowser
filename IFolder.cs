using System.Collections;

namespace MidiBrowser
{
	internal interface IFolder
	{
		IEnumerable Items { get; }
	}
}