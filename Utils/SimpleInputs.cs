internal class SimpleInputs
{
    private const float TriggerThreshold = 0.5f;

    public static bool RightTrigger => ControllerInputPoller.instance.rightControllerIndexFloat > TriggerThreshold;
    public static bool RightGrab => ControllerInputPoller.instance.rightGrab;
    public static bool RightA => ControllerInputPoller.instance.rightControllerSecondaryButton;
    public static bool RightB => ControllerInputPoller.instance.rightControllerSecondaryButton;
    public static bool LeftTrigger => ControllerInputPoller.instance.leftControllerIndexFloat > TriggerThreshold;
    public static bool LeftGrab => ControllerInputPoller.instance.leftGrab;
    public static bool LeftX => ControllerInputPoller.instance.leftControllerPrimaryButton;
    public static bool LeftY => ControllerInputPoller.instance.leftControllerSecondaryButton;
}
