using UnityEngine;

public class OutfitTestCaller : MonoBehaviour
{
    public OutfitController outfit;

    [TextArea(3, 10)]
    public string json = "{\"topKey\":\"TOP_TEST_001\",\"bottomKey\":\"BOTTOM_TEST_001\",\"topColor\":\"#FF0000\",\"bottomColor\":\"#0000FF\"}";


    [TextArea(3, 10)]
    public string json1 = "{\"topKey\":\"TOP_TEST_002\",\"bottomKey\":\"BOTTOM_TEST_002\",\"topColor\":\"#0000FF\",\"bottomColor\":\"#FF0000\"}";
    private void Update()
    {
        if (outfit == null) return;

        if (Input.GetKeyDown(KeyCode.T))
        {
            outfit.ApplyOutfitJson(json);

        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            outfit.ApplyOutfitJson(json1);
        }
    }
}