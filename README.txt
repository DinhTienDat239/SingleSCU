SINGLE CENTRAL UPDATE ON UNITY
I wrote this script for learning.
Replace Singleton DP of your own.
If you need c#7 and lower for syntax error. Please checkout c#7 branch

Example use:

private SCUManager.SCUSubscription _sub;

void OnEnable()
{
    _sub = SCUManager.Instance.Register(DoSth, 2, SCUManager.SCUUpdateType.Update);
}

void OnDisable()
{
    _sub.Dispose();
}

void DoSth(int number)
{
    speed += number;
}
