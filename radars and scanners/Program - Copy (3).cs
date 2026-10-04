//Basic Settings: 
using Sandbox.Game.GameSystems;
using System;
using System.Collections.Generic;
using VRageMath;

string lcdName = "Text panel";
string remoteName = "Remote Control";
int remoteRange = 100;                 // choose how far to scan in meters, up to 50km in theory  
int iRPM = 18;                         // choose how fast the line rotates     

//Advanced Settings: 
int refreshRate = 20;               // script runs per second, for performance reasons. 60 for realtime. 
float fontSize = 0.4f;                 // choose fontsize, 0.3f to see everything    
float remoteBase = 0.5f;                //try decreasing if you can't detect anything 
int stepRange = 100;                //add or remove this much with arguments "increase" or "decrease" 



// Not Settings  
IMyTextPanel lcd;
IMyRemoteControl remote;
bool initialized = false;
int ticks = 0;
string[] matrix;
bool detecting = false;
double distance = 0;
int center = 25;
double[] linex = new double[25];
double[] liney = new double[25];
double[] oldlinex = new double[25];
double[] oldliney = new double[25];
List<string> scanData = new List<string>();
int rotorOffset = -90;
int iTimer = 0;
int dAStep = 6;
List<Vector3D> vecDims = new List<Vector3D>();
double iAngle = 0;
double iAngle2 = 0;


//Thank you Skleroz  
Vector3D GetVector(Vector3D vecFront, Vector3D vecPoM, double dFrontAng, double dPoMAng)
{
    Vector3D vecRes = vecFront;
    if (vecFront.Dot(vecPoM) < 0.001 && Math.Abs(Math.Cos(dFrontAng)) + Math.Abs(Math.Cos(dPoMAng)) > 0.995)
    {
        vecFront = vecFront * Math.Cos(dFrontAng);
        vecPoM = vecPoM * Math.Cos(dPoMAng);
        vecRes = Vector3D.Add(vecFront, vecPoM);
        vecRes.Normalize();
    }
    return vecRes;
}


void Render(string str)
{
    lcd.WritePublicText(str);
    lcd.ShowTextureOnScreen();
    lcd.ShowPublicTextOnScreen();
}


void Plot(int x, int y, char c)
{
    if (x >= 0 && y >= 0 && x < 50 && y < 50)
    {
        x = x * 2;
        char[] chars = matrix[y].ToCharArray();
        chars[x] = c;
        matrix[y] = new string(chars);
    }

}

void ArgumentHandler(string args)
{
    args = args.ToLower();

    if (args == "increase")
    {
        remoteRange = remoteRange + stepRange;
    }
    if (args == "decrease")
    {
        if (remoteRange - stepRange > 0)
            remoteRange = remoteRange - stepRange;
    }

}


double GetDistance(Vector3D target)
{
    Vector3D myPos = remote.GetPosition();
    double distance = (target - myPos).Length();
    return distance;
}


void Init()
{

    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(lcdName, blocks);
    if (blocks.Count == 0) GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(blocks);
    lcd = blocks[0] as IMyTextPanel;

    GridTerminalSystem.SearchBlocksOfName(remoteName, blocks);
    if (blocks.Count == 0) GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(blocks);
    remote = blocks[0] as IMyRemoteControl;

    lcd.SetValue<float>("FontSize", fontSize);

    if (matrix == null) matrix = new string[50];
    for (int a = 0; a < 50; a++)
    {
        matrix[a] = "";
        for (int b = 0; b < 50; b++)
        {
            matrix[a] += "x_";
        }
        matrix[a] += "\n";
    }
    Refresh();
    initialized = true;
}

void Refresh()
{
    string output = "Max Distance: " + remoteRange.ToString() + " m --- Target Distance: " + ((int)distance).ToString() + " m --- Detecting: " + detecting.ToString() + "\n";
    for (int i = 0; i < scanData.Count; i++)
    {
        int tempx = Int32.Parse(scanData[i].Split(':')[0]);
        int tempy = Int32.Parse(scanData[i].Split(':')[1]);
        Plot(tempx, tempy, 'x');
    }
    for (int i = 0; i < 50; i++)
    {
        output += matrix[i];
    }
    Render(output);
}

//I stole this from Pennywise, customized for my purposes by Skleroz        
Vector3D RemotePos(Vector3D vecF, float Distance, float Base = 1.0f)
{
    Vector3D MyPos, O1, O2, F1, F2, F3;
    MyPos = remote.GetPosition();
    if (vecF.GetDim(0) == 0)
    {
        O1 = MyPos + remote.WorldMatrix.Forward;
    }
    else
    {
        vecF.Normalize();
        O1 = MyPos + vecF;
    }
    if (Base == 1.0f)
    {
        Base = Distance * (float)Math.Asin(10 / (float)iRPM / 180 * Math.PI) * 2.0f;
    }

    F1 = remote.GetFreeDestination(O1, Distance, Base);
    if (O1 != F1)
    {
        O1 = Vector3D.Normalize(O1 - MyPos) * (F1 - MyPos).Length() + MyPos;
        O2 = Vector3D.Normalize(F1 - O1);
        F2 = remote.GetFreeDestination(O1 - O2, Distance, Base);
        F3 = remote.GetFreeDestination(O1 - (O2 * 2), Distance, Base);
        double sig;
        return CircleBy3Points(F1, F2, F3);
    }
    else
    {
        return MyPos + (O1 - MyPos) * Distance;
    }
}

//I stole this from Pennywise              
Vector3D CircleBy3Points(Vector3D p1, Vector3D p2, Vector3D p3)
{
    Vector3D Center = new Vector3D(0, 0, 0);
    Vector3D t = p2 - p1;
    Vector3D u = p3 - p1;
    Vector3D v = p3 - p2;
    Vector3D w = Vector3D.Cross(t, u);
    double wsl = Math.Pow(w.Length(), 2);
    if (wsl < 10e-14)
    {
        return Center;
    }
    else
    {
        double iwsl2 = 1.0 / (2.0 * wsl);
        double tt = Vector3D.Dot(t, t);
        double uu = Vector3D.Dot(u, u);
        Center = p1 + (u * tt * Vector3D.Dot(u, v) - t * uu * Vector3D.Dot(t, v)) * iwsl2;
        return Center;
    }
}

void Main(string arguments)
{
    if (arguments != "") ArgumentHandler(arguments);

    if (initialized == false) Init();

    double dRPM = 60 * 60 / iRPM;
    if (iTimer >= dRPM)
    {
        iTimer = 0;
    }
    else
    {
        iTimer = iTimer + 1;
    }

    //Thank you Skleroz     
    iAngle = 2.0f * Math.PI * iTimer / dRPM + MathHelper.ToRadians(rotorOffset);
    iAngle2 = 2.0f * Math.PI * iTimer / dRPM;

    ticks++;
    if (ticks == 60 / refreshRate)
    {
        ticks = 0;
        Vector3D vecCheck = GetVector(remote.WorldMatrix.Forward, remote.WorldMatrix.Right, iAngle2, iAngle);
        distance = GetDistance(RemotePos(vecCheck, (float)remoteRange, remoteBase));

        if (distance >= remoteRange - 5)
        {
            detecting = false;
        }
        else
        {
            detecting = true;
        }

        if (detecting == true)
        {
            double x = center + ((Math.Cos(iAngle) * distance) / (remoteRange / 25));  //divide to downsize ratio, to fit on screen           
            double y = center + ((Math.Sin(iAngle) * distance) / (remoteRange / 25));
            string storeLine = ((int)x).ToString() + ":" + ((int)y).ToString();
            if (scanData.Count == 0)
            {
                scanData.Add(storeLine);
            }
            else if (scanData[scanData.Count - 1] != storeLine)
            {
                scanData.Add(storeLine);
            }
        }
        if (detecting == false || scanData.Count > 2500)
        {
            if (scanData.Count > 0) scanData.Clear();
        }

    }//ticks 

    for (int i = 0; i < 25; i++)
    {
        linex[i] = center + (Math.Cos(iAngle) * i);
        liney[i] = center + (Math.Sin(iAngle) * i);
        Plot((int)oldlinex[i], (int)oldliney[i], '_');
        Plot((int)linex[i], (int)liney[i], 'x');
        oldlinex[i] = linex[i];
        oldliney[i] = liney[i];
    }

    Refresh();

}