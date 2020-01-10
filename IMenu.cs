namespace MidiBrowser
{
	internal interface IMenu
	{
		string[] MenuItems { get; }

		void Execute(string menuItem);
	}
}