// https://forum.botengine.org/t/bot-7e301560-ver-1-1-orca-drones-space-1-0-0-8/1442
/* Ver: 1.1 ORCA DRONES: SPACE 1.0-0.8
 * Should work with a porpoise too... test with that first... and never nice drones!

This bot mines ore only with drones. It using a bookmark folder for chosen belts and take them in order. It unload the ore in "home" station.
Before running this bot, prepare the EVE online client as follows:
+ Set the UI language to english.
+ Move your mining ship to a solar system which has asteroid belts and at least one station in which you can dock.
+ In the Overview, create a preset which includes asteroids and rats and enter the name of that preset in the configuration section below at 'OverviewPreset'. 
+    The bot will make sure this preset is loaded when it needs to use the overview.
+ Set Overview to sort by distance with the nearest entry at the top.
+ In the Inventory, select the 'List' view.
+ Enable the info panel 'System info'. The bot needs this to find asteroid belts and stations.
+ Arrange windows to not occlude ship modules or info panels.
+ Create your own bookmark "home" (not sure what this is for)
+ Create a bookmark in the regular folder and name it whatever you want, but it is the unload location. Update the name of the variable UnloadBookmark with whatever you name yours.
+ Create your own Folder for mining Belts
 ( for now it should be named "OrcaMiningFolder" If you change it, change also in script settings. )
+ Create your own bookmarks for mining and put them INSIDE of folder

+ Preparations of bot:
+ fill/change the MiningRange (15/18 are probably good because the bot will approach them anyway the the drones won't have to fly that far the whole time)
+ DroneNumber
+ At what lvl of ore hold you go to unload
This script is based on Terpla && Viir scripts and adapted 
*/

using BotSharp.ToScript.Extension;
using MemoryStruct = Sanderling.Interface.MemoryStruct;
using Parse = Sanderling.Parse;

//	Begin of configuration section ->

//	The bot loads this preset to the active tab in the Overview window.
string OverviewPreset = "Porpmining";

//	Activate shield hardener.
var ActivateHardener = true;

//mining range (in metric)
int MiningRange = 18000;
// drones
int DroneNumber = 5;

//the name of folder of bookmarks for mining
string FolderBeltsMining = "OrcaMiningFolder";

//	Name of the container to unload to as shown in inventory.
string UnloadDestContainerName = "Item Hangar";

//	Bookmark of location where ore should be unloaded.
string UnloadBookmark = "Unload";

//	Percentage of fill level at which to enter the offload process.
var EnterOffloadOreContainerFillPercent = 98;

//	Bot will switch mining site when rats are visible and shield hitpoints are lower than this value.
var SwitchMiningSiteHitpointThresholdPercent = 95;

var EmergencyWarpOutHitpointPercent = 90;


//	Bookmark of place to retreat to to prevent ship loss.
string RetreatBookmark = UnloadBookmark;

const string StatusStringFromDroneEntryTextRegexPattern = @"\((.*)\)";
static public string StatusStringFromDroneEntryText(this string droneEntryText) => droneEntryText?.RegexMatchIfSuccess(StatusStringFromDroneEntryTextRegexPattern)?.Groups[1]?.Value?.RemoveXmlTag()?.Trim();


bool returnDronesToBayOnRetreat = true;
//	<- End of configuration section


Func<object> BotStopActivity = () => null;

Func<object> NextActivity = MainStep;

Queue<string> visitedLocations = new Queue<string>();

// Set up a random number generator to enable selecting a random asteroid belt
// NOTE: This might ruin the whole "memory" thing, but it was jacked up anyway so hopefully this will help.
System.Random rand = new System.Random();

for(;;)
{
	var stepBeginTimeMilli = Host.GetTimeContinuousMilli();

MemoryUpdate();

Host.Log(
		"Stats: ore container fill: " + OreContainerFillPercent + "%" +
		", mining range from settings: " + MiningRange +
		", shield.hp: " + ShieldHpPercent + "%" +
		", retreat: " + RetreatReason + 
		", JLA: " + JammedLastAge +
		", overview.roids: " + ListAsteroidOverviewEntry?.Length +
		", offload count: " + OffloadCount +
		", nextAct: " + NextActivity?.Method?.Name);

	CloseModalUIElement();

	if(0 < RetreatReason?.Length && !(Measurement?.IsDocked ?? false))
	{
	Host.Log("1 ");
		if ((returnDronesToBayOnRetreat)  && (0 != DronesInSpaceCount))
		 DroneEnsureInBay();
		 
	if (!returnDronesToBayOnRetreat || (returnDronesToBayOnRetreat && 0 == DronesInSpaceCount))
	{
	Host.Log("2 ");
		ClickMenuEntryOnPatternMenuRoot(Measurement?.InfoPanelCurrentSystem?.ListSurroundingsButton, RetreatBookmark, "Dock");
	}
		continue;
	}

	NextActivity = NextActivity?.Invoke() as Func<object>;

	if(BotStopActivity == NextActivity)
		break;

	if(null == NextActivity)
		NextActivity = MainStep;

	Host.Delay((int) Math.Max(0, 1000 - (Host.GetTimeContinuousMilli() - stepBeginTimeMilli)));
}

bool? ShipHasOreHold
{
    get
    {
        var inventoryActiveShipEntry = WindowInventory?.ActiveShipEntry;

        //	If the tree entry for the ship is not expanded....
        if (!(IsExpanded(inventoryActiveShipEntry) ?? false))
            return null;    // Then I do not know if there is an ore hold.

        return inventoryActiveShipEntry?.TreeEntryFromCargoSpaceType(ShipCargoSpaceTypeEnum.OreHold) != null;
    }
}


//	seconds since ship was jammed.
long? JammedLastAge => Jammed ? 0 : (Host.GetTimeContinuousMilli() - JammedLastTime) / 1000;

int? ShieldHpPercent => ShipUi?.HitpointsAndEnergy?.Shield / 10;

bool ShouldSwitchMiningSite =>
    !(Measurement?.IsDocked ?? false) &&
    !(SwitchMiningSiteHitpointThresholdPercent < ShieldHpPercent) || JammedLastAge < 10;

bool OreContainerFilledForOffload => Math.Max(0, Math.Min(100, EnterOffloadOreContainerFillPercent)) <= OreContainerFillPercent;

Int64? JammedLastTime = null;
string RetreatReasonTemporary = null;
string RetreatReasonPermanent = null;
string RetreatReason => RetreatReasonPermanent ?? RetreatReasonTemporary;
int? LastCheckOreContainerFillPercent = null;

int OffloadCount = 0;

Func<object> MainStep()
{
    if (Measurement?.IsDocked ?? false)
    {
        InInventoryUnloadItems();
        Host.Log("cargo hold unloaded ");
        if (0 < RetreatReasonPermanent?.Length)
            return BotStopActivity;

        if (0 < RetreatReason?.Length)
        {
            Host.Log("return to mainstep ");
            return MainStep;
        }
        Undock();
    }

    EnsureOverviewTypeSelectionLoaded();

    EnsureWindowInventoryOreContainerIsOpen();

    if (ReadyForManeuver)
    {
        DroneEnsureInBay();
        Host.Log("ready");
        if (OreContainerFilledForOffload)
        {
            Host.Log("I'm full, go to unload");
            DroneEnsureInBay();
            if (ReadyForManeuver)
                ClickMenuEntryOnPatternMenuRoot(Measurement?.InfoPanelCurrentSystem?.ListSurroundingsButton, RetreatBookmark, "Dock");
            return MainStep;
        }

        if (!(0 < ListAsteroidOverviewEntry?.Length) || ShouldSwitchMiningSite)
        {
            Host.Log("warping ");
            InitiateWarpToMiningSite();
        }

        ModuleMeasureAllTooltip();

        if (ActivateHardener)
            ActivateHardenerExecute();

    }
    return InBeltMineStep;
}

void CloseModalUIElement()
{
    var ButtonClose =
        ModalUIElement?.ButtonText?.FirstOrDefault(button => (button?.Text).RegexMatchSuccessIgnoreCase("close|no|ok"));

    Sanderling.MouseClickLeft(ButtonClose);
}
void DroneLaunch()
{
    Host.Log("launch drones.");
    Sanderling.MouseClickRight(DronesInBayListEntry);
    Sanderling.MouseClickLeft(Menu?.FirstOrDefault()?.EntryFirstMatchingRegexPattern("launch", RegexOptions.IgnoreCase));
}

void DroneEnsureInBay()
{
    if (0 == DronesInSpaceCount)
        return;
    DroneReturnToBay();
    Host.Delay(4444);
}

void DroneReturnToBay()
{
    Host.Log("return drones to bay.");
    Sanderling.MouseClickRight(DronesInSpaceListEntry);
    Sanderling.MouseClickLeft(Menu?.FirstOrDefault()?.EntryFirstMatchingRegexPattern("return.*bay", RegexOptions.IgnoreCase));
    // Sanderling.KeyboardPressCombined(new[]{ targetLockedKeyCode, VirtualKeyCode.VK_R });//if you like 
}

Func<object> InBeltMineStep()
{
    var droneListView = Measurement?.WindowDroneView?.FirstOrDefault()?.ListView;

    var droneGroupWithNameMatchingPattern = new Func<string, DroneViewEntryGroup>(namePattern =>
        droneListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(group => group?.LabelTextLargest()?.Text?.RegexMatchSuccessIgnoreCase(namePattern) ?? false));


    var droneGroupInLocalSpace = droneGroupWithNameMatchingPattern("local space");

    var setDroneInLocalSpace = droneListView?.Entry?.OfType<DroneViewEntryItem>()
        ?.Where(drone => droneGroupInLocalSpace?.RegionCenter()?.B < drone?.RegionCenter()?.B)
        ?.ToArray();
    var droneInLocalSpaceSetStatus =
        setDroneInLocalSpace?.Select(drone => drone?.LabelText?.Select(label => label?.Text?.StatusStringFromDroneEntryText()))?.ConcatNullable()?.WhereNotDefault()?.Distinct()?.ToArray();

    var droneInLocalSpaceIdle =
        droneInLocalSpaceSetStatus?.Any(droneStatus => droneStatus.RegexMatchSuccessIgnoreCase("idle")) ?? false;

    var droneGroupInBay = droneGroupWithNameMatchingPattern("bay");

    if (ShouldSwitchMiningSite)
        return MainStep;
    EnsureWindowInventoryOreContainerIsOpen();

    EnsureOverviewTypeSelectionLoaded();

    if (OreContainerFilledForOffload)
        return null;

    var setTargetAsteroidInRange =
        SetTargetAsteroid?.Where(target => target?.DistanceMax <= MiningRange)?.ToArray();

    var setTargetAsteroidInRangeNotAssigned =
        setTargetAsteroidInRange?.Where(target => !(0 < target?.Assigned?.Length))?.ToArray();

    Host.Log("targeted asteroids in range (without assignment): " + setTargetAsteroidInRange?.Length + " (" + setTargetAsteroidInRangeNotAssigned?.Length + ")");

    if (0 < setTargetAsteroidInRangeNotAssigned?.Length)
    {
        var targetAsteroidInputFocus =
            setTargetAsteroidInRangeNotAssigned?.FirstOrDefault(target => target?.IsSelected ?? false);

        if (null == targetAsteroidInputFocus)
            Sanderling.MouseClickLeft(setTargetAsteroidInRangeNotAssigned?.FirstOrDefault());
        if (0 < DronesInBayCount && DronesInSpaceCount < DroneNumber)
            DroneLaunch();

        if (!(0 < DronesInSpaceCount))
            DroneLaunch();
        //MineTarget();
        //	 Sanderling.KeyboardPress(VirtualKeyCode.VK_F);
        if (droneInLocalSpaceIdle && (Measurement?.Target?.Length > 0))
        {
            Host.Log("drones idle");
            //MineTarget();
            Sanderling.KeyboardPress(VirtualKeyCode.VK_F);
            Host.Log("mining a new asteroid");
        }
        return InBeltMineStep;
    }

    var asteroidOverviewEntryNext = ListAsteroidOverviewEntry?.FirstOrDefault();
    var asteroidOverviewEntryNextNotTargeted = ListAsteroidOverviewEntry?.FirstOrDefault(entry => !((entry?.MeTargeted ?? false) || (entry?.MeTargeting ?? false)));

    Host.Log("next asteroid: (" + asteroidOverviewEntryNext?.Name + " , distance: " + asteroidOverviewEntryNext?.DistanceMax + ")" +
        ", next asteroid not targeted: (" + asteroidOverviewEntryNext?.Name + " , distance: " + asteroidOverviewEntryNext?.DistanceMax + ")");

    if (null == asteroidOverviewEntryNext)
    {
        Host.Log("no asteroid available");
        return null;
    }

    if (null == asteroidOverviewEntryNextNotTargeted)
    {
        Host.Log("all asteroids targeted");
        return null;
    }

    if (!(asteroidOverviewEntryNextNotTargeted.DistanceMax < MiningRange))
    {
        if (!(1111 < asteroidOverviewEntryNext?.DistanceMin))
        {
            Host.Log("distance between asteroids too large");
            return null;
        }

        Host.Log("out of range, approaching");
        ClickMenuEntryOnMenuRoot(asteroidOverviewEntryNext, "approach");
    }
    else
    {
        Host.Log("initiate lock asteroid");
        ClickMenuEntryOnMenuRoot(asteroidOverviewEntryNextNotTargeted, "^lock");
    }

    return InBeltMineStep;
}


Sanderling.Parse.IMemoryMeasurement Measurement =>
    Sanderling?.MemoryMeasurementParsed?.Value;

IWindow ModalUIElement =>
    Measurement?.EnumerateReferencedUIElementTransitive()?.OfType<IWindow>()?.Where(window => window?.isModal ?? false)
    ?.OrderByDescending(window => window?.InTreeIndex ?? int.MinValue)
    ?.FirstOrDefault();

IEnumerable<Parse.IMenu> Menu => Measurement?.Menu;

Parse.IShipUi ShipUi => Measurement?.ShipUi;

bool Jammed => ShipUi?.EWarElement?.Any(EwarElement => (EwarElement?.EWarType).RegexMatchSuccess("electronic")) ?? false;

Sanderling.Interface.MemoryStruct.IMenuEntry MenuEntryLockTarget =>
    Menu?.FirstOrDefault()?.Entry?.FirstOrDefault(entry => entry.Text.RegexMatchSuccessIgnoreCase("^lock"));

Sanderling.Parse.IWindowOverview WindowOverview =>
    Measurement?.WindowOverview?.FirstOrDefault();

Sanderling.Parse.IWindowInventory WindowInventory =>
    Measurement?.WindowInventory?.FirstOrDefault();
IWindowDroneView WindowDrones =>
    Measurement?.WindowDroneView?.FirstOrDefault();

DroneViewEntryGroup DronesInBayListEntry =>
    WindowDrones?.ListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(Entry => null != Entry?.Caption?.Text?.RegexMatchIfSuccess(@"Drones in bay", RegexOptions.IgnoreCase));

DroneViewEntryGroup DronesInSpaceListEntry =>
    WindowDrones?.ListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(Entry => null != Entry?.Caption?.Text?.RegexMatchIfSuccess(@"Drones in Local Space", RegexOptions.IgnoreCase));

int? DronesInSpaceCount => DronesInSpaceListEntry?.Caption?.Text?.AsDroneLabel()?.Status?.TryParseInt();
int? DronesInBayCount => DronesInBayListEntry?.Caption?.Text?.AsDroneLabel()?.Status?.TryParseInt();

ITreeViewEntry InventoryActiveShipOreContainer
{
    get
    {
        var hasOreHold = ShipHasOreHold;

        if (hasOreHold == null)
            return null;

        return
            WindowInventory?.ActiveShipEntry?.TreeEntryFromCargoSpaceType(
                hasOreHold.Value ? ShipCargoSpaceTypeEnum.OreHold : ShipCargoSpaceTypeEnum.General);
    }
}

IInventoryCapacityGauge OreContainerCapacityMilli =>
    (InventoryActiveShipOreContainer?.IsSelected ?? false) ? WindowInventory?.SelectedRightInventoryCapacityMilli : null;

int? OreContainerFillPercent => (int?)((OreContainerCapacityMilli?.Used * 100) / OreContainerCapacityMilli?.Max);

Tab OverviewPresetTabActive =>
    WindowOverview?.PresetTab
    ?.OrderByDescending(tab => tab?.LabelColorOpacityMilli ?? 0)
    ?.FirstOrDefault();

string OverviewTypeSelectionName =>
    WindowOverview?.Caption?.RegexMatchIfSuccess(@"\(([^\)]*)\)")?.Groups?[1]?.Value;

Parse.IOverviewEntry[] ListAsteroidOverviewEntry =>
    WindowOverview?.ListView?.Entry
    ?.Where(entry => null != OreTypeFromAsteroidName(entry?.Name))
    ?.OrderBy(entry => entry.DistanceMax ?? int.MaxValue)
    ?.ToArray();


bool ReadyForManeuverNot =>
    Measurement?.ShipUi?.Indication?.LabelText?.Any(indicationLabel =>
        (indicationLabel?.Text).RegexMatchSuccessIgnoreCase("warp|docking")) ?? false;

bool ReadyForManeuver => !ReadyForManeuverNot && !(Measurement?.IsDocked ?? true);

Sanderling.Parse.IShipUiTarget[] SetTargetAsteroid =>
    Measurement?.Target?.Where(target =>
        target?.TextRow?.Any(textRow => textRow.RegexMatchSuccessIgnoreCase("asteroid")) ?? false)?.ToArray();

Sanderling.Interface.MemoryStruct.IListEntry WindowInventoryItem =>
    WindowInventory?.SelectedRightInventory?.ListView?.Entry?.FirstOrDefault();

//	extract the ore type from the name as seen in overview. "Asteroid (Plagioclase)"
string OreTypeFromAsteroidName(string AsteroidName) =>
    AsteroidName.ValueFromRegexMatchGroupAtIndex(@"Asteroid \(([^\)]+)", 0);

void ClickMenuEntryOnMenuRoot(IUIElement MenuRoot, string MenuEntryRegexPattern)
{
    Sanderling.MouseClickRight(MenuRoot);

    var Menu = Measurement?.Menu?.FirstOrDefault();

    var MenuEntry = Menu?.EntryFirstMatchingRegexPattern(MenuEntryRegexPattern, RegexOptions.IgnoreCase);

    Sanderling.MouseClickLeft(MenuEntry);
}

void ClickMenuEntryOnPatternMenuRoot(IUIElement MenuRoot, string MenuEntryRegexPattern, string SubMenuEntryRegexPattern = null)
{
    Sanderling.MouseClickRight(MenuRoot);
    var Menu = Sanderling?.MemoryMeasurementParsed?.Value?.Menu?.FirstOrDefault();
    var MenuEntry = Menu?.EntryFirstMatchingRegexPattern(MenuEntryRegexPattern, RegexOptions.IgnoreCase);
    Sanderling.MouseClickLeft(MenuEntry);
    if (SubMenuEntryRegexPattern != null)
    {
        // Using the API explorer when we click on the top menu we get another menu that has more options
        // So skip the MenuRoot and click on Submenu
        // var subMenu = Sanderling?.MemoryMeasurementParsed?.Value?.Menu?.Skip(1).First();
        var subMenu = Sanderling?.MemoryMeasurementParsed?.Value?.Menu?.ElementAtOrDefault(1);
        var subMenuEntry = subMenu?.EntryFirstMatchingRegexPattern(SubMenuEntryRegexPattern, RegexOptions.IgnoreCase);
        Host.Log("initiate warp to '" + MenuEntryRegexPattern + "'");
        Sanderling.MouseClickLeft(subMenuEntry);
    }
}

void ModuleMeasureAllTooltip()
{
    Host.Log("measure tooltips of all modules.");

    for (; ; )
    {
        var NextModule = Sanderling.MemoryMeasurementAccu?.Value?.ShipUiModule?.FirstOrDefault(m => null == m?.TooltipLast);

        if (null == NextModule)
            break;

        Host.Log("measure module.");
        //	take multiple measurements of module tooltip to reduce risk to keep bad read tooltip.
        Sanderling.MouseMove(NextModule);
        Sanderling.WaitForMeasurement();
        Sanderling.MouseMove(NextModule);
    }
}

void ActivateHardenerExecute()
{
    var SubsetModuleHardener =
        Sanderling.MemoryMeasurementAccu?.Value?.ShipUiModule
        ?.Where(module => module?.TooltipLast?.Value?.IsHardener ?? false);

    var SubsetModuleToToggle =
        SubsetModuleHardener
        ?.Where(module => !(module?.RampActive ?? false));

    foreach (var Module in SubsetModuleToToggle.EmptyIfNull())
        ModuleToggle(Module);
}

void ModuleToggle(Sanderling.Accumulation.IShipUiModule Module)
{
    var ToggleKey = Module?.TooltipLast?.Value?.ToggleKey;

    Host.Log("toggle module using " + (null == ToggleKey ? "mouse" : Module?.TooltipLast?.Value?.ToggleKeyTextLabel?.Text));

    if (null == ToggleKey)
        Sanderling.MouseClickLeft(Module);
    else
        Sanderling.KeyboardPressCombined(ToggleKey);
}

void EnsureWindowInventoryOpen()
{
    if (null != WindowInventory)
        return;

    Host.Log("open Inventory.");
    Sanderling.MouseClickLeft(Measurement?.Neocom?.InventoryButton);
}

void EnsureWindowInventoryOreContainerIsOpen()
{
    EnsureWindowInventoryOpen();

    var inventoryActiveShip = WindowInventory?.ActiveShipEntry;

    if (InventoryActiveShipOreContainer == null && !(IsExpanded(inventoryActiveShip) ?? false))
    {
        Host.Log("It looks like the active ships entry in the inventory is not expanded. I try to expand it to see if the ship has an ore hold.");
        Sanderling.MouseClickLeft(inventoryActiveShip?.ExpandToggleButton);
    }

    if (!(InventoryActiveShipOreContainer?.IsSelected ?? false))
        Sanderling.MouseClickLeft(InventoryActiveShipOreContainer);
}

//	sample label text: Intensive Reprocessing Array <color=#66FFFFFF>1,123 m</color>
string InventoryContainerLabelRegexPatternFromContainerName(string containerName) =>
    @"^\s*" + Regex.Escape(containerName) + @"\s*($|\<)";

void InInventoryUnloadItems() => InInventoryUnloadItemsTo(UnloadDestContainerName);

void InInventoryUnloadItemsTo(string DestinationContainerName)
{
    Host.Log("unload items to '" + DestinationContainerName + "'.");

    EnsureWindowInventoryOreContainerIsOpen();

    for (; ; )
    {
        var oreContainerListItem = WindowInventory?.SelectedRightInventory?.ListView?.Entry?.ToArray();

        var oreContainerItem = oreContainerListItem?.FirstOrDefault();

        if (null == oreContainerItem)
            break;    //    0 items in the container which holds the ore.

        if (1 < oreContainerListItem?.Length)
            ClickMenuEntryOnMenuRoot(oreContainerItem, @"select\s*all");

        var DestinationContainerLabelRegexPattern =
            InventoryContainerLabelRegexPatternFromContainerName(DestinationContainerName);

        var DestinationContainer =
            WindowInventory?.LeftTreeListEntry?.SelectMany(entry => new[] { entry }.Concat(entry.EnumerateChildNodeTransitive()))
            ?.FirstOrDefault(entry => entry?.Text?.RegexMatchSuccessIgnoreCase(DestinationContainerLabelRegexPattern) ?? false);

        if (null == DestinationContainer)
            Host.Log("error: Inventory entry labeled '" + DestinationContainerName + "' not found");

        Sanderling.MouseDragAndDrop(oreContainerItem, DestinationContainer);
    }
}

bool InitiateWarpToMiningSite() =>
    InitiateDockToOrWarpToLocationInSolarSystemMenu(FolderBeltsMining, PickNextMiningSiteFromSystemMenu);

//MemoryStruct.IMenuEntry PickNextMiningSiteFromSystemMenu(IReadOnlyList<MemoryStruct.IMenuEntry> availableMenuEntries)
//{
//    Host.Log("I am seeing " + availableMenuEntries?.Count.ToString() + " mining sites to choose from.");

//    var nextSite =
//        availableMenuEntries
//        ?.OrderBy(menuEntry => visitedLocations.ToList().IndexOf(menuEntry?.Text))
//        ?.FirstOrDefault();

//    Host.Log("I pick '" + nextSite?.Text + "' as next mining site, based on the intent to rotate through the mining sites and recorded previous locations.");
//    return nextSite;
//}

MemoryStruct.IMenuEntry PickNextMiningSiteFromSystemMenu(IReadOnlyList<MemoryStruct.IMenuEntry> availableMenuEntries)
{
    int cnt = 0;
    if (availableMenuEntries?.Count == null)
    {
        // TOOD: bad    
    }
    else if ((availableMenuEntries.Count == 0))
    {
        // TODO: bad 
    }
    else
    {
        cnt = availableMenuEntries.Count;
    }

    Host.Log("I am seeing " + availableMenuEntries?.Count.ToString() + " mining sites to choose from.");

    var nextSite =
        availableMenuEntries
        ?.OrderBy(menuEntry => visitedLocations.ToList().IndexOf(menuEntry?.Text))
        ?.ElementAtOrDefault(rand.Next(cnt));

    Host.Log("I pick '" + nextSite?.Text + "' as next mining site, based on the intent to rotate through the mining sites and recorded previous locations.");
    return nextSite;
}

bool InitiateDockToOrWarpToLocationInSolarSystemMenu(
    string submenuLabel,
    Func<IReadOnlyList<MemoryStruct.IMenuEntry>, MemoryStruct.IMenuEntry> pickPreferredDestination = null)
{
    Host.Log("Attempt to initiate dock to or warp to menu entry in submenu '" + submenuLabel + "'");

    var listSurroundingsButton = Measurement?.InfoPanelCurrentSystem?.ListSurroundingsButton;

    Sanderling.MouseClickRight(listSurroundingsButton);

    var submenuEntry = Measurement?.Menu?.FirstOrDefault()?.EntryFirstMatchingRegexPattern("^" + submenuLabel + "$", RegexOptions.IgnoreCase);

    if (null == submenuEntry)
    {
        Host.Log("Submenu '" + submenuLabel + "' not found in the solar system menu.");
        return true;
    }

    Sanderling.MouseClickLeft(submenuEntry);

    var submenu = Measurement?.Menu?.ElementAtOrDefault(1);

    var destinationMenuEntry = pickPreferredDestination?.Invoke(submenu?.Entry?.ToList()) ?? submenu?.Entry?.FirstOrDefault();

    if (destinationMenuEntry == null)
    {
        Host.Log("Failed to open submenu '" + submenuLabel + "' in the solar system menu.");
        return true;
    }

    Sanderling.MouseClickLeft(destinationMenuEntry);

    var actionsMenu = Measurement?.Menu?.ElementAtOrDefault(2);

    if (destinationMenuEntry == null)
    {
        Host.Log("Failed to open actions menu for '" + destinationMenuEntry.Text + "' in the solar system menu.");
        return true;
    }

    var dockMenuEntry = actionsMenu?.EntryFirstMatchingRegexPattern("dock", RegexOptions.IgnoreCase);
    var warpMenuEntry = actionsMenu?.EntryFirstMatchingRegexPattern(@"warp.*within.*m", RegexOptions.IgnoreCase);
    var approachEntry = actionsMenu?.EntryFirstMatchingRegexPattern(@"approach", RegexOptions.IgnoreCase);

    var maneuverMenuEntry = dockMenuEntry ?? warpMenuEntry;

    if (null != maneuverMenuEntry)
    {
        Host.Log("initiating '" + maneuverMenuEntry.Text + "' on '" + destinationMenuEntry?.Text + "'");
        Sanderling.MouseClickLeft(maneuverMenuEntry);
        return false;
    }

    if (null != approachEntry)
    {
        Host.Log("found menu entry '" + approachEntry.Text + "'. Assuming we are already there.");
        return false;
    }

    Host.Log("no suitable menu entry found on '" + destinationMenuEntry?.Text + "'");
    return true;
}

void Undock()
{
    while (Measurement?.IsDocked ?? true)
    {
        ///////////OLD CODE
        //Sanderling.MouseClickLeft(Measurement?.WindowStation?.FirstOrDefault()?.UndockButton);
        //Host.Log("waiting for undocking to complete.");
        //Host.Delay(8000);
        ///////////END OLD CODE
        ///
        
		// TODO: It appears that UndockButton (built in to Sanderling) no longer works out of the box (we can still find it though as seen above)
        //Sanderling.MouseClickLeft(Measurement?.WindowStation?.FirstOrDefault()?.UndockButton);

        // get the window that is the station controls
        var ws = Measurement?.WindowStation?.FirstOrDefault();

        // within that window find a button with the text that looks like undock
        var ButtonUndock = ws.ButtonText?.FirstOrDefault(button => (button?.Text).RegexMatchSuccessIgnoreCase("undock"));
        Sanderling.MouseClickLeft(ButtonUndock);

        Host.Log("waiting for undocking to complete.");
        Host.Delay(8000);
		     

    }

    Host.Delay(4444);
    Sanderling.InvalidateMeasurement();
}



void EnsureOverviewTypeSelectionLoaded()
{
    if (null == OverviewPresetTabActive || null == WindowOverview || null == OverviewPreset)
        return;

    if (string.Equals(OverviewTypeSelectionName, OverviewPreset, StringComparison.OrdinalIgnoreCase))
        return;

    Host.Log("loading preset '" + OverviewPreset + "' to overview (current selection is '" + OverviewTypeSelectionName + "').");
    Sanderling.MouseClickRight(OverviewPresetTabActive);
    Sanderling.MouseClickLeft(Menu?.FirstOrDefault()?.EntryFirstMatchingRegexPattern("load.*preset", RegexOptions.IgnoreCase));
    var PresetMenuEntry = Menu?.ElementAtOrDefault(1)?.EntryFirstMatchingRegexPattern(@"^\s*" + Regex.Escape(OverviewPreset) + @"\s*$", RegexOptions.IgnoreCase);

    if (null == PresetMenuEntry)
    {
        Host.Log("error: menu entry '" + OverviewPreset + "' not found");
        return;
    }

    Sanderling.MouseClickLeft(PresetMenuEntry);
}

void DelayWhileUpdatingMemory(int delayAmountMilli)
{
    var beginTimeMilli = Host.GetTimeContinuousMilli();

    while (true)
    {
        var remainingTimeMilli = beginTimeMilli + delayAmountMilli - Host.GetTimeContinuousMilli();
        if (remainingTimeMilli < 0)
            break;

        Host.Delay(Math.Min(1000, (int)remainingTimeMilli));
        Sanderling.InvalidateMeasurement();
        MemoryUpdate();
    }
}

void MemoryUpdate()
{
    RetreatUpdate();
    JammedLastTimeUpdate();
    OffloadCountUpdate();
    UpdateLocationRecord();
}

void UpdateLocationRecord()
{
    //	I am not interested in locations which are only close during warp.
    if (Measurement?.ShipUi?.Indication?.ManeuverType == ShipManeuverTypeEnum.Warp)
        return;

    // Purpose of recording locations is to prioritize our next destination when warping to mining site or docking to station.
    // For this purpose, I will compare the recorded locations with the menu entries in the system menu.
    // Therefore I want the format of the recorded location to be the same as it appears in the menu entries in the system menu.

    var currentSystemLocationLabelText =
        Measurement?.InfoPanelCurrentSystem?.ExpandedContent?.LabelText
        ?.OrderByCenterVerticalDown()?.FirstOrDefault()?.Text;

    if (currentSystemLocationLabelText == null)
        return;

    // 2018-03 observed label text: <url=showinfo:15//40088644 alt='Nearest'>Amsen V - Asteroid Belt 1</url>

    var currentLocationName = RegexExtension.RemoveXmlTag(currentSystemLocationLabelText)?.Trim();

    var lastRecordedLocation = visitedLocations.LastOrDefault();

    if (lastRecordedLocation == currentLocationName)
        return;

    visitedLocations.Enqueue(currentLocationName);
    Host.Log("Recorded transition from location '" + lastRecordedLocation + "' to location '" + currentLocationName + "'");

    if (100 < visitedLocations.Count)
        visitedLocations.Dequeue();
}

/*
2018-03 Observation: Station name containing reference to moon appears different between system menu and system info panel.
In the info panel 'Moon 17' was used while in the system menu it was 'M17'.
*/
public bool StationFromSystemInfoPanelEqualsStationFromSystemMenu(
    string stationNameInCurrentSystemInfoPanel,
    string stationNameInSystemMenu)
{
    //	Copied from https://github.com/botengine-de/Optimat.EO/blob/6c19e8f36e30d5468e94d627eb16dcb78bb47d12/src/Optimat.EveOnline.Bot/Sonst/AgentUndMission.Aktualisiire.cs#L1888-L1893

    var representationPattern =
        Regex.Replace(
        stationNameInCurrentSystemInfoPanel,
        "Moon\\s*",
        "M([\\w]*\\s*)",
        RegexOptions.IgnoreCase);

    return
        stationNameInSystemMenu.RegexMatchSuccessIgnoreCase(representationPattern);
}

void JammedLastTimeUpdate()
{
    if (Jammed)
        JammedLastTime = Host.GetTimeContinuousMilli();
}

bool MeasurementEmergencyWarpOutEnter =>
    !(Measurement?.IsDocked ?? false) && !(EmergencyWarpOutHitpointPercent < ShieldHpPercent);

void RetreatUpdate()
{
    //RetreatReasonTemporary = (RetreatOnNeutralOrHostileInLocal && hostileOrNeutralsInLocal)	? "hostile or neutral in local" : null;
    //RetreatReasonTemporary = "shield hp";
    if (!MeasurementEmergencyWarpOutEnter)
        return;

    //	measure multiple times to avoid being scared off by noise from a single measurement. 
    Sanderling.InvalidateMeasurement();

    if (!MeasurementEmergencyWarpOutEnter)
        return;

    RetreatReasonPermanent = "shield hp";
}

void OffloadCountUpdate()
{
    var OreContainerFillPercentSynced = OreContainerFillPercent;

    if (!OreContainerFillPercentSynced.HasValue)
        return;

    if (0 == OreContainerFillPercentSynced && OreContainerFillPercentSynced < LastCheckOreContainerFillPercent)
        ++OffloadCount;

    LastCheckOreContainerFillPercent = OreContainerFillPercentSynced;
}

void MineTarget()
{
    Sanderling.MouseClickRight(DronesInSpaceListEntry);
    Sanderling.MouseClickLeft(Menu?.FirstOrDefault()?.EntryFirstMatchingRegexPattern("^Mine Repeatedly", RegexOptions.IgnoreCase));
}
bool? IsExpanded(IInventoryTreeViewEntryShip shipEntryInInventory) =>
    shipEntryInInventory == null ? null :
    (bool?)((shipEntryInInventory.IsExpanded ?? false) || 0 < (shipEntryInInventory.Child?.Count() ?? 0));