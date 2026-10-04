//Basic settings:   
using Sandbox.Game.GameSystems;
using System;
using System.Collections.Generic;
using VRageMath;

string lcdName = "Text panel";
string sensorName = "Sensor";           // suggested settings: front 50m, left and right 3m, top and bottom 5m, back 1m         
string rotorName = "Rotor";             // recommended rotation ~10-20rpm   

//Long range settings:  
bool remoteMode = false;                 // long range scanning using remote control block.      
string remoteName = "Remote Control";    // if remoteMode = true   
int remoteRange = 500;                  // if remoteMode = true choose how far to scan   
int stepRange = 100;                    // if remoteMode = true, increase or decrease this much remoteRange with arguments   
int iRPM = 10;                         // if remoteMode = true, choose how fast the line rotates     

//Advanced settings:   
int refreshRate = 20;               // script runs per second, for performance reasons. 60 for realtime.   
float fontSize = 0.4f;                 // choose fontsize, 0.3f to see everything    
bool upsideDown = false;                // set this to true if your rotor is upside down   
float remoteBase = 0.5f;                //try decreasing if you can't detect anything    


//Not settings   
IMyTextPanel lcd;
IMyMotorStator rotor;
IMySensorBlock sensor;
IMyRemoteControl remote;
bool initialized = false;
int ticks = 0;
string[] matrix;
bool detecting = false;
int sensorRange = 0;
int rotorOffset = -90;
double distance = 0;
int center = 25;
double[] linex = new double[25];
double[] liney = new double[25];
double[] oldlinex = new double[25];
double[] oldliney = new double[25];
List<string> scanData = new List<string>();
int iTimer = 0;
int dAStep = 6;
List<Vector3D> vecDims = new List<Vector3D>();
double iAngle = 0;
double iAngle2 = 0;

void Render(string str)
{
    lcd.WritePublicText(str);
    lcd.ShowTextureOnScreen();
    lcd.ShowPublicTextOnScreen();
}

void ScanSensor()
{
    var entity = sensor.LastDetectedEntity;
    if (entity != null)
    {
        detecting = true;
        distance = GetDistance(entity.GetPosition());
    }
    else
    {
        detecting = false;
    }
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
    if (args == "switch")
    {
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(blocks);
        if (blocks.Count > 0)
        {
            if (remoteMode == true) remoteMode = false;
            else remoteMode = true;
        }
    }
}

double GetDistance(Vector3D target)
{
    Vector3D myPos = sensor.GetPosition();
    double distance = (target - myPos).Length();
    return distance;
}

void Init()
{

    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(rotorName, blocks);
    if (blocks.Count == 0) GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(blocks);
    rotor = blocks[0] as IMyMotorStator;

    GridTerminalSystem.SearchBlocksOfName(lcdName, blocks);
    if (blocks.Count == 0) GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(blocks);
    lcd = blocks[0] as IMyTextPanel;

    GridTerminalSystem.SearchBlocksOfName(sensorName, blocks);
    if (blocks.Count == 0) GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(blocks);
    sensor = blocks[0] as IMySensorBlock;


    GridTerminalSystem.SearchBlocksOfName(remoteName, blocks);
    if (blocks.Count == 0) GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(blocks);
    if (blocks.Count > 0) remote = blocks[0] as IMyRemoteControl;


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
    string output = "";
    if (remoteMode == true) output = "Scan Range: " + remoteRange.ToString() + " m --- Target Distance: " + ((int)distance).ToString() + "m --- Detecting: " + detecting.ToString() + "\n";
    if (remoteMode == false) output = "Target Distance: " + ((int)distance).ToString() + " m --- Detecting: " + detecting.ToString() + "\n";
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
Vector3D RemotePos(Vector3D vecF, float Distance, float Base)
{
    Vector3D MyPos, O1, O2, F1, F2, F3;
    MyPos = sensor.GetPosition();
    if (vecF.GetDim(0) == 0)
    {
        O1 = MyPos + remote.WorldMatrix.Forward;
    }
    else
    {
        vecF.Normalize();
        O1 = MyPos + vecF;
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
        return MyPos + (remote.WorldMatrix.Forward * Distance);
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


void Main(string arguments)
{
    if (arguments != "") ArgumentHandler(arguments);

    if (initialized == false) Init();

    if (remoteMode == false)
    {
        iAngle = rotor.Angle + MathHelper.ToRadians(rotorOffset);
        if (upsideDown) iAngle = MathHelper.ToRadians(180) - iAngle;
    }
    if (remoteMode == true)
    {
        double dRPM = 60 * 60 / iRPM;
        if (iTimer >= dRPM)
        {
            iTimer = 0;
        }
        else
        {
            iTimer = iTimer + 1;
        }

        //A virtual pretend ghost rotor that doesn't exist. Thanks Skleroz.  
        iAngle = 2.0f * Math.PI * iTimer / dRPM + MathHelper.ToRadians(rotorOffset);
        iAngle2 = 2.0f * Math.PI * iTimer / dRPM;
    }

    ticks++;
    if (ticks == 60 / refreshRate)
    {
        ticks = 0;

        if (remoteMode == false)
        {
            sensorRange = (int)sensor.FrontExtend;
            ScanSensor();
        }
        if (remoteMode == true)
        {
            sensorRange = remoteRange;
            Vector3D vecCheck = GetVector(remote.WorldMatrix.Forward, remote.WorldMatrix.Right, iAngle2, iAngle);
            distance = GetDistance(RemotePos(vecCheck, (float)remoteRange, remoteBase));
            if (distance >= sensorRange - 10)
            { //Have to cut 10m from range due to false positives  
                detecting = false;
            }
            else
            {
                detecting = true;
            }
        }

        if (detecting == true)
        {
            double x = center + ((Math.Cos(iAngle) * distance) / (sensorRange / 25));  //divide to downsize ratio, to fit on screen         
            double y = center + ((Math.Sin(iAngle) * distance) / (sensorRange / 25));
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