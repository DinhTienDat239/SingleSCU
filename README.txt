I wrote this script for learning.
Replace Singleton DP of your own.

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
