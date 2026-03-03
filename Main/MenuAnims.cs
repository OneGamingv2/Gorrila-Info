using UnityEngine;
using GorillaInfo;

public class MenuAnimations
{
    private static readonly Vector3 OpenScale = Vector3.one * 0.18f;
    private const float ScaleSmoothTime = 0.08f;
    private const float OpenSnapThresholdSqr = 0.000001f;
    private const float CloseSnapThresholdSqr = 0.00001f;
    private Vector3 _scaleVelocity;

    public void openanim()
    {
        AudioHelper.PlaySound("open.wav");
        GorillaInfoMain main = GorillaInfoMain.Instance;
        main.menuState = GorillaInfoMain.MenuState.Opening;
        main.menuLoader.menuInstance.SetActive(true);
    }

    public void closinganim()
    {
        AudioHelper.PlaySound("close.wav");
        GorillaInfoMain.Instance.menuState = GorillaInfoMain.MenuState.Closing;
    }

    public void animshandler()
    {
        GorillaInfoMain main = GorillaInfoMain.Instance;
        GameObject menu = main.menuLoader.menuInstance;
        if (menu == null) return;

        Transform t = menu.transform;
        bool opening = main.menuState == GorillaInfoMain.MenuState.Opening;
        Vector3 target = opening ? OpenScale : Vector3.zero;

        t.localScale = Vector3.SmoothDamp(t.localScale, target, ref _scaleVelocity, ScaleSmoothTime);

        if (opening)
        {
            if ((t.localScale - OpenScale).sqrMagnitude < OpenSnapThresholdSqr)
            {
                t.localScale = OpenScale;
                main.menuState = GorillaInfoMain.MenuState.Open;
            }
        }
        else
        {
            if (t.localScale.sqrMagnitude < CloseSnapThresholdSqr)
            {
                t.localScale = Vector3.zero;
                _scaleVelocity = Vector3.zero;
                menu.SetActive(false);
                main.menuState = GorillaInfoMain.MenuState.Closed;
            }
        }
    }
}
