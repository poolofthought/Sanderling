namespace Sanderling
{
	public enum ShipManeuverTypeEnum
	{
		None = 0,
		Stop = 9,
		Approach = 10,
		Orbit = 11,
		KeepAtRange = 12,
		Dock = 17,
		Docked = 18,
		Undock = 19,
		Warp = 30,
		Jump = 31,

	}

	public enum ShipCargoSpaceTypeEnum
	{
		None = 0,
		General = 1,
		DroneBay = 3,
		OreHold = 7,
	}

	public enum OreTypeEnum
	{
		None = 0,
		Arkonor = 100, // moon ore
		Bistot = 110,
		Crokite = 120,
		Dark_Ochre = 130,
		Gneiss = 140,
		Hedbergite = 150,
		Hemorphite = 160,
		Jaspet = 170,
		Kernite = 180,
		Mercoxit = 190,
		Omber = 200,
		Otavite = 205, // uncommon moon ore
		Plagioclase = 210,
		Pollucite = 215, // rare moon ore
		Pyroxeres = 220,
		Scordite = 230,
		Spodumain = 240,
		Titanite = 245, // common moon
		Veldspar = 250,
	}
}
