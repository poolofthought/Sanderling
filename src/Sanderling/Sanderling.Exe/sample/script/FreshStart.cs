
//default empty bot - could be fun to test when no mid mining:
//    From: https://forum.botengine.org/t/empty-bot-for-testing-and-learning/2268
//    For my testings with Sanderling i have clean up a Bot for Testing and learning Bot functions.
//    I think it is very important to understand single Sanderling functions and so i have clean up the Miningbot to a single Working Loop with only a “Lost Connection function”.
//    TestScript

using BotSharp.ToScript.Extension;
using MemoryStruct = Sanderling.Interface.MemoryStruct;
using Parse = Sanderling.Parse;

//	begin of configuration section for variables ->

//	<- end of configuration section

// ################################### Script  START ##########################################

Func<object> BotStopActivity = () => null;

Func<object> NextActivity = Step1;

//Loop
for(;;)
{
	Host.Log(
		"nextAct: " + NextActivity?.Method?.Name);
		
	NextActivity = NextActivity?.Invoke() as Func<object>;
	
	if(BotStopActivity == NextActivity)
	{
		Host.Log("BotStop, no Next Activity?");
		break;
	}

	if(null == NextActivity)
		NextActivity = Step1;
}

// Steps for Working
Func<object> Step1()
{
	//Start
	Host.Log("Step1 start...");

	//##### Check Connection #####
	//Load Sanderlin infos in Variable "Measurement"
	Sanderling.Parse.IMemoryMeasurement Measurement = Sanderling?.MemoryMeasurementParsed?.Value;

	// Search for Word in "Measurement"
	var ConnectionLost = Measurement?.WindowOther?.FirstOrDefault()?.LabelText?.FirstOrDefault(text => (text?.Text.RegexMatchSuccessIgnoreCase("Connection") ?? false));

	if (ConnectionLost != null) //When found than...
	{
		Host.Log(" ***              Lost connection at : " + DateTime.Now.ToString(" HH:mm:ss") + " ***   ");
		Console.Beep(1000, 200); Console.Beep(800, 250); Console.Beep(600, 350);
		return BotStopActivity;
	}
	else //if not than...
	{
		Host.Log(" ***              Bot Running at : " + DateTime.Now.ToString(" HH:mm:ss") + " ***   ");
	}
	//##### Check Connection #####

	// do what
	DoSomething();

	//Wait a while
	Host.Delay(3000);

	// Jump back to Start
	return NextActivity;
}

// Make Things wich can use in Steps
void DoSomething()
{

	Host.Log("DoSomething...");

}