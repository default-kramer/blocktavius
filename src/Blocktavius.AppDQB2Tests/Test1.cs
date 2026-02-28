using Blocktavius.AppDQB2.Persistence.V1;

namespace Blocktavius.AppDQB2Tests;

[TestClass]
public sealed class Test1
{
	[TestMethod]
	public void TestMethod1()
	{
		const string json =
			"""
			{
			  "ProfileVerificationHash": "SwsLltjL\u002B/NQCgJQGj9kgJhDXTrXlTXHz7cI/fgxuII=",
			  "SourceSlot": {
			    "SlotNumber": 3,
			    "SlotName": "Slot 3 (B02)"
			  },
			  "DestSlot": {
			    "SlotNumber": 1,
			    "SlotName": "Slot 1 (B00)"
			  },
			  "SourceStgdatFilename": "STGDAT16.BIN",
			  "ChunkExpansion": [],
			  "Notes": "",
			  "Images": [],
			  "MinimapVisible": true,
			  "ChunkGridVisible": true,
			  "Scripts": [
			    {
			      "ScriptName": "Script 1",
			      "ScriptNodes": [
			        {
			          "$type": "bad discriminator",
			          "Elevation": 74,
			          "HillDesigner": {
			            "$type": "WinsomeHill-4380",
			            "Steepness": 2
			          },
			          "AreaPersistId": "IMG:export\\plateaus\\p1.png",
			          "BlockPersistId": "BLK:12",
			          "LockRandomSeed": false
			        }
			      ]
			    }
			  ],
			  "SelectedScriptIndex": 11
			}
			""";

		var project = ProjectV1.Load(json);
		Assert.IsNotNull(project);
	}
}
