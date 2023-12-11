namespace fobot.POCOs;

public class FoodClickCallbackModel
{
    public string CallbackFunctionName { get; set; }
    public bool IsGarnishIncluded { get; set; }
    public bool IsFlavoringIncluded { get; set; }
    public Action<int, int> CallbackFunction { get; set; }
}