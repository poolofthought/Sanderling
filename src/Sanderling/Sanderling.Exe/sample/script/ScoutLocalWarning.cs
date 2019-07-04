// From: https://pastebin.com/Aau9yjtD
// Also: https://forum.botengine.org/t/scout-and-or-local-warning-script/2210
// Scount / and or local warning bot


using BotSharp.ToScript.Extension;
using Parse = Sanderling.Parse;
using System.Timers;
using BotEngine.Interface;
// If the action is to WARN you: This script will warn you on a defined (by you) chan if neutrals or
//ennemies are in the scout current system and (if not docked or tethered) start warping between safe spots randomly 
//during the neutral presence. (random planet or asteroid by default). It warns you by both anoing console beep AND 
//write a predefined text in your peronnal intel chan, to make it blink so it visualy warns you on your main.
// If the action is to WATCH: While not docked, in warp or tethered, the script will play a sound to warn you if a 
//neutral or ennemy has entered your system.

// Be sure to have the local chan solo and visible. Be sure your intel chan is visible both on your scout AND your main... ("blink on" strongly recommended )
// If you have a safe spot folder, replace the "Ateroid Belts","Planets" folder by its name.
// Create your private chan, join it with your scout(s) and your main and define it as your PersonnalIntel.
// Start and fly safe.

//		What do you want the script to do for you?		//
//WARN your main account with a scout placed in an other system or WATCH at your Local and beep when an ennemy enters yous system. 
string Action = "WATCH"; // Here enter WARN or WATCH
						 //		Where do you want the script to pick your warping spots?		//
string[] SafeSpots = { "Asteroid Belts", "Planets" };
//		Where do the script watch and where it warns 		//
string PersonnalIntel = "PersonnalIntel";
string ChanToScout = "Local";
//		What do you want the script to write to warn you		//
string WarningText = "Neutral Or Ennemy in System Dagobah";


//	//	//		No need to go further if you don't want to tcustomize the script		//	//	//


bool Tethered => Sanderling?.MemoryMeasurementParsed?.Value?.ShipUi?.EWarElement?.Any(status => (status?.EWarType).RegexMatchSuccess("tethering")) ?? false;
bool ReadyForManeuverNot => Sanderling?.MemoryMeasurementParsed?.Value?.ShipUi?.Indication?.LabelText?.Any(indicationLabel => (indicationLabel?.Text).RegexMatchSuccessIgnoreCase("warp|docking")) ?? false;
bool ReadyForManeuver => !ReadyForManeuverNot && !(Sanderling?.MemoryMeasurementParsed?.Value?.IsDocked ?? true);
bool WarpOut = false;
bool EmergencyWarpOutEnabled = false;

WindowChatChannel ChanLocal => Sanderling.MemoryMeasurementParsed?.Value?.WindowChatChannel?.FirstOrDefault(windowChat => windowChat?.Caption?.RegexMatchSuccessIgnoreCase(ChanToScout) ?? false);
WindowChatChannel ChanPersonnalIntel => Sanderling.MemoryMeasurementParsed?.Value?.WindowChatChannel?.FirstOrDefault(windowChat => windowChat?.Caption?.RegexMatchSuccessIgnoreCase(PersonnalIntel) ?? false);

Sanderling.Interface.MemoryStruct.IChatParticipantEntry[] Ennemies => ChanLocal?.Participant?.Where(entry => !entry?.FlagIcon?.Any(flagIcon => new[] { "good standing", "excellent standing", "Pilot is in your (alliance|fleet|corporation)", }.Any(goodStandingText => flagIcon?.HintText?.RegexMatchSuccessIgnoreCase(goodStandingText) ?? false)) ?? false)?.ToArray() ?? null;
bool IsNeutralOrEnemy(IChatParticipantEntry participantEntry) => !(participantEntry?.FlagIcon?.Any(flagIcon => new[] { "good standing", "excellent standing", "Pilot is in your (alliance|fleet|corporation)", }.Any(goodStandingText => flagIcon?.HintText?.RegexMatchSuccessIgnoreCase(goodStandingText) ?? false)) ?? false);
bool hostileOrNeutralsInLocal => 1 != ChanLocal?.ParticipantView?.Entry?.Count(IsNeutralOrEnemy);
bool MeasurementEmergencyWarpOutEnter => hostileOrNeutralsInLocal;

Func<object> BotStopActivity = () => null;
Func<object> NextActivity = MainStep;
for(;;)
{	if(null == NextActivity)
		NextActivity = MainStep;
	NextActivity = NextActivity?.Invoke() as Func<object>;
}

Func<object> MainStep()
{
	EmergencyWarpOutUpdate();
	if (EmergencyWarpOutEnabled)
	{ Host.Log("neutrals or ennemies"); Host.Delay(variation()); }
	else
	{ Host.Delay(variation(909, 1212)); }

	Sanderling.InvalidateMeasurement();
	Sanderling.WaitForMeasurement();
	if (ShipManeuverTypeEnum.Warp == Sanderling?.MemoryMeasurementParsed?.Value?.ShipUi?.Indication?.ManeuverType)
		WarpOut = true;

	if (WarpOut == true)
	{
		Host.Log("warping out");
	loop:
		Host.Delay(variation(222, 333));
		Sanderling.InvalidateMeasurement();
		Sanderling.WaitForMeasurement();
		if (ShipManeuverTypeEnum.Warp == Sanderling?.MemoryMeasurementParsed?.Value?.ShipUi?.Indication?.ManeuverType)
		{ goto loop; }  //	do nothing while warping	
		WarpOut = false;
	}

	if (EmergencyWarpOutEnabled && !Tethered && !(Sanderling?.MemoryMeasurementParsed?.Value.IsDocked ?? true))
		switch (Action)
		{
			case "WARN":
				Turn();
				break;
			case "WATCH":
				Console.Beep(1500, 200); Host.Delay(150); Console.Beep(1500, 200); Host.Delay(variation(800, 1200));
				break;
			default:
				Turn();
				break;
		}

	return MainStep;
}

Random rand => new System.Random();
int variation(int min = 175, int max = 350) { return rand.Next(min, max); }

T RandomElement<T>(IEnumerable<T> seq)
{
	var counted = seq?.ToArray();
	if (!(0 < counted?.Length))
		return default(T);
	return counted[rand.Next(counted.Length)];
}

bool Turn()
{
	Host.Log("Turning");
	var ListSurroundingsButton = Sanderling.MemoryMeasurementParsed?.Value?.InfoPanelCurrentSystem?.ListSurroundingsButton;
	Sanderling.MouseClickRight(ListSurroundingsButton);
	Host.Delay(variation());
	string SSFolderBookmark = RandomElement(SafeSpots);
	var FolderMenuEntry = Sanderling.MemoryMeasurementParsed?.Value?.Menu?.ElementAtOrDefault(0)?.EntryFirstMatchingRegexPattern("^" + SSFolderBookmark + "$", RegexOptions.IgnoreCase);
	Sanderling.MouseClickLeft(FolderMenuEntry);
	Host.Delay(variation());
	int NbSF = Sanderling.MemoryMeasurementParsed?.Value?.Menu?.ElementAtOrDefault(1)?.Entry?.Count() ?? 0;
	var BookmarkMenuEntry = Sanderling.MemoryMeasurementParsed?.Value?.Menu?.ElementAtOrDefault(1)?.Entry?.ElementAtOrDefault(variation(0, NbSF));
	Sanderling.MouseClickLeft(BookmarkMenuEntry);
	Host.Delay(variation());
	var Menu = Sanderling?.MemoryMeasurementParsed?.Value?.Menu?.Last();
	var WarpMenuEntry = Menu?.EntryFirstMatchingRegexPattern(@"warp.*within\s*0", RegexOptions.IgnoreCase);
	if (null == WarpMenuEntry)
	{ return false; }
	Host.Delay(variation());
	Sanderling.MouseClickLeft(WarpMenuEntry);

	WarpOut = true;
	return true;
}

void EmergencyWarpOutUpdate()
{
	if (!MeasurementEmergencyWarpOutEnter)
	{
		EmergencyWarpOutEnabled = false;
		return;
	}
	Sanderling.InvalidateMeasurement();
	Sanderling.WaitForMeasurement();
	if (!MeasurementEmergencyWarpOutEnter)
	{
		EmergencyWarpOutEnabled = false;
		return;
	}

	var WarnZone = ChanPersonnalIntel?.MessageInput;
	if (!EmergencyWarpOutEnabled)
	{
		if (Action == "WARN")
		{
			Console.Beep(1500, 2200);
			Sanderling.MouseClickLeft(WarnZone);
			Sanderling.TextEntry(WarningText);
			Sanderling.KeyboardPress(VirtualKeyCode.RETURN);
		}
	}

	EmergencyWarpOutEnabled = true;
}