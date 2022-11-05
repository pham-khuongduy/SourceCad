// Decompiled with JetBrains decompiler
// Type: AutoCADPlugin.Main
// Assembly: OffsetPointbyProjection, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 667DC8F7-EA3D-4D31-AB3D-AF8F8ACF5DC3
// Assembly location: C:\Users\phamh\Desktop\Arent\OffsetPointByProjection.dll

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace AutoCADPlugin
{
  public static class Main
  {
    [CommandMethod("CMDGP")]
    public static void CMDCR()
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      Database database = mdiActiveDocument.get_Database();
      Editor editor = mdiActiveDocument.get_Editor();
      PromptEntityOptions promptEntityOptions = new PromptEntityOptions("\n Chọn 3D Polyline ");
      promptEntityOptions.SetRejectMessage("\n Chọn 3D Polyline");
      promptEntityOptions.AddAllowedClass(typeof (Polyline3d), true);
      PromptEntityResult entity = editor.GetEntity(promptEntityOptions);
      if (((PromptResult) entity).get_Status() != 5100)
        return;
      PromptPointResult point = editor.GetPoint("\n Vui lòng chọn một điểm");
      using (Transaction transaction = database.get_TransactionManager().StartTransaction())
      {
        Polyline3d polyline3D = transaction.GetObject(entity.get_ObjectId(), (OpenMode) 0) as Polyline3d;
        
        // kiểm tra trong class Main điểm point từ người dùng chọn có nằm trong đường polyline3D ko
        if (!Main.CheckPointOnPolyline(polyline3D, point.get_Value()))
        {
          int num = (int) MessageBox.Show(" Vị trí của điểm được chọn nằm ngoài đối tượng. ", "Lỗi");
          transaction.Commit();
        }
        else
        {
          PromptStringOptions promptStringOptions = new PromptStringOptions("\n Nhập độ dài hình chiếu");
          promptStringOptions.set_AllowSpaces(true);
          PromptResult promptResult = mdiActiveDocument.get_Editor().GetString(promptStringOptions);
          double result = 0.0;
          if (!double.TryParse(promptResult.get_StringResult(), out result))
          {
            int num = (int) MessageBox.Show("Vui lòng nhập một số. ", "Lỗi");
          }
          else
          {
            Main.FindPointWithPolyline(polyline3D, point.get_Value(), result);
            transaction.Commit();
          }
        }
      }
    }

    private static bool IsPointOnPolyline(Polyline pl, Point3d pt)
    {
      bool flag = false;
      for (int index = 0; index < pl.get_NumberOfVertices(); ++index)
      {
        Curve3d curve3d = (Curve3d) null;
        SegmentType segmentType = pl.GetSegmentType(index);
        if (segmentType == 1)
          curve3d = (Curve3d) pl.GetArcSegmentAt(index);
        else if (segmentType == 0)
          curve3d = (Curve3d) pl.GetLineSegmentAt(index);
        if (DisposableWrapper.op_Inequality((DisposableWrapper) curve3d, (DisposableWrapper) null))
        {
          flag = curve3d.IsOn(pt);
          if (flag)
            break;
        }
      }
      return flag;
    }

    public static List<ObjectId> CreateLoftedSurface(Spline spline)
    {
      List<ObjectId> objectIdList = new List<ObjectId>();
      Database workingDatabase = HostApplicationServices.get_WorkingDatabase();
      Editor editor = Application.get_DocumentManager().get_MdiActiveDocument().get_Editor();
      Transaction transaction = workingDatabase.get_TransactionManager().StartTransaction();
      using (transaction)
      {
        BlockTable blockTable = (BlockTable) transaction.GetObject(workingDatabase.get_BlockTableId(), (OpenMode) 0);
        BlockTableRecord blockTableRecord = (BlockTableRecord) transaction.GetObject(((SymbolTable) blockTable).get_Item((string) BlockTableRecord.ModelSpace), (OpenMode) 1);
        Extents3d geometricExtents1 = ((Entity) spline).get_GeometricExtents();
        Point3d maxPoint = ((Extents3d) ref geometricExtents1).get_MaxPoint();
        Extents3d geometricExtents2 = ((Entity) spline).get_GeometricExtents();
        Point3d minPoint = ((Extents3d) ref geometricExtents2).get_MinPoint();
        Line line1 = new Line(new Point3d(((Point3d) ref minPoint).get_X() - 5000.0, ((Point3d) ref minPoint).get_Y() - 5000.0, 0.0), new Point3d(((Point3d) ref minPoint).get_X() - 5000.0, ((Point3d) ref maxPoint).get_Y() + 50000.0, 0.0));
        blockTableRecord.AppendEntity((Entity) line1);
        transaction.AddNewlyCreatedDBObject((DBObject) line1, true);
        LoftProfile loftProfile1 = new LoftProfile((Entity) line1);
        Line line2 = new Line(new Point3d(((Point3d) ref maxPoint).get_X() + 5000.0, ((Point3d) ref maxPoint).get_Y() + 5000.0, 0.0), new Point3d(((Point3d) ref maxPoint).get_X() + 5000.0, ((Point3d) ref minPoint).get_Y() - 50000.0, 0.0));
        blockTableRecord.AppendEntity((Entity) line2);
        transaction.AddNewlyCreatedDBObject((DBObject) line2, true);
        LoftProfile loftProfile2 = new LoftProfile((Entity) line2);
        ObjectId loftedSurface = Surface.CreateLoftedSurface(new LoftProfile[2]
        {
          loftProfile1,
          loftProfile2
        }, (LoftProfile[]) null, (LoftProfile) null, new LoftOptions(), true);
        objectIdList.Add(loftedSurface);
        objectIdList.Add(((DBObject) line1).get_ObjectId());
        objectIdList.Add(((DBObject) line2).get_ObjectId());
        transaction.Commit();
        editor.Regen();
      }
      return objectIdList;
    }

    public static void FindPointWithSpline(Spline spline, Point3d pt, double distanceInput)
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      Database database = mdiActiveDocument.get_Database();
      Editor editor = mdiActiveDocument.get_Editor();
      using (Transaction transaction = database.get_TransactionManager().StartTransaction())
      {
        BlockTable blockTable = transaction.GetObject(database.get_BlockTableId(), (OpenMode) 0) as BlockTable;
        BlockTableRecord blockTableRecord = transaction.GetObject(((SymbolTable) blockTable).get_Item((string) BlockTableRecord.ModelSpace), (OpenMode) 1) as BlockTableRecord;
        List<ObjectId> loftedSurface = Main.CreateLoftedSurface(spline);
        Spline spline1 = ((IEnumerable<Entity>) ((Surface) (transaction.GetObject(loftedSurface[0], (OpenMode) 1) as LoftedSurface)).ProjectOnToSurface((Entity) spline, Vector3d.get_ZAxis())).ToList<Entity>()[0] as Spline;
        ((Curve) spline1).ReverseCurve();
        double distAtPoint = ((Curve) spline1).GetDistAtPoint(new Point3d(((Point3d) ref pt).get_X(), ((Point3d) ref pt).get_Y(), 0.0));
        Point3d pointAtDist = ((Curve) spline1).GetPointAtDist(distAtPoint + distanceInput);
        Line line1 = new Line(((Point3d) ref pointAtDist).Add(new Vector3d(0.0, 0.0, 500000000.0)), ((Point3d) ref pointAtDist).Add(new Vector3d(0.0, 0.0, -500000000.0)));
        Vector3d secondDerivative = ((Curve) spline1).GetSecondDerivative(pointAtDist);
        Vector3d vector3d = ((Vector3d) ref secondDerivative).Negate();
        Line line2 = new Line(((Point3d) ref pointAtDist).Add(Vector3d.op_Multiply(vector3d, 1000.0)), ((Point3d) ref pointAtDist).Add(Vector3d.op_Multiply(secondDerivative, 1000.0)));
        blockTableRecord.AppendEntity((Entity) line2);
        transaction.AddNewlyCreatedDBObject((DBObject) line2, true);
        Point3dCollection point3dCollection = new Point3dCollection();
        ((Entity) line1).IntersectWith((Entity) spline, (Intersect) 0, point3dCollection, IntPtr.Zero, IntPtr.Zero);
        blockTableRecord.AppendEntity((Entity) line1);
        transaction.AddNewlyCreatedDBObject((DBObject) line1, true);
        blockTableRecord.AppendEntity((Entity) spline1);
        transaction.AddNewlyCreatedDBObject((DBObject) spline1, true);
        DBPoint dbPoint1 = new DBPoint(pointAtDist);
        blockTableRecord.AppendEntity((Entity) dbPoint1);
        transaction.AddNewlyCreatedDBObject((DBObject) dbPoint1, true);
        if ((uint) point3dCollection.get_Count() > 0U)
        {
          double num = ((Curve) spline).GetDistAtPoint(point3dCollection.get_Item(0)) - ((Curve) spline).GetDistAtPoint(pt);
          editor.WriteMessage("\n khoảng cách:" + num.ToString());
          Clipboard.SetText(num.ToString());
          using (DBPoint dbPoint2 = new DBPoint(point3dCollection.get_Item(0)))
          {
            blockTableRecord.AppendEntity((Entity) dbPoint2);
            transaction.AddNewlyCreatedDBObject((DBObject) dbPoint2, true);
            database.set_Pdmode(34);
            database.set_Pdsize(100.0);
          }
        }
        else
          editor.WriteMessage("\nTôi không thể tìm ra điểm ...");
        transaction.Commit();
      }
    }

    public static bool CheckPointOnPolyline(Polyline3d polyline3D, Point3d pt)
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      Database database = mdiActiveDocument.get_Database();
      mdiActiveDocument.get_Editor();
      using (Transaction transaction = database.get_TransactionManager().StartTransaction())
      {
        BlockTable blockTable = transaction.GetObject(database.get_BlockTableId(), (OpenMode) 0) as BlockTable;
        BlockTableRecord blockTableRecord = transaction.GetObject(((SymbolTable) blockTable).get_Item((string) BlockTableRecord.ModelSpace), (OpenMode) 1) as BlockTableRecord;
        List<Point3d> point3dList = new List<Point3d>();
        Polyline pl = new Polyline();
        IEnumerator enumerator = polyline3D.GetEnumerator();
        try
        {
          while (enumerator.MoveNext())
          {
            ObjectId current = (ObjectId) enumerator.Current;
            PolylineVertex3d polylineVertex3d = (PolylineVertex3d) transaction.GetObject(current, (OpenMode) 0);
            point3dList.Add(polylineVertex3d.get_Position());
          }
        }
        finally
        {
          (enumerator as IDisposable)?.Dispose();
        }
        for (int index = 0; index < point3dList.Count; ++index)
        {
          Polyline polyline = pl;
          int num = index;
          Point3d point3d = point3dList[index];
          double x = ((Point3d) ref point3d).get_X();
          point3d = point3dList[index];
          double y = ((Point3d) ref point3d).get_Y();
          Point2d point2d = new Point2d(x, y);
          polyline.AddVertexAt(num, point2d, 0.0, 0.0, 0.0);
        }
        return Main.IsPointOnPolyline(pl, new Point3d(((Point3d) ref pt).get_X(), ((Point3d) ref pt).get_Y(), 0.0));
      }
    }

    public static void FindPointWithPolyline(
      Polyline3d polyline3D,
      Point3d pt,
      double distanceInput)
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      Database database = mdiActiveDocument.get_Database();
      Editor editor = mdiActiveDocument.get_Editor();
      using (Transaction transaction = database.get_TransactionManager().StartTransaction())
      {
        BlockTable blockTable = transaction.GetObject(database.get_BlockTableId(), (OpenMode) 0) as BlockTable;
        BlockTableRecord blockTableRecord = transaction.GetObject(((SymbolTable) blockTable).get_Item((string) BlockTableRecord.ModelSpace), (OpenMode) 1) as BlockTableRecord;
        List<Point3d> point3dList = new List<Point3d>();
        Polyline polyline1 = new Polyline();
        IEnumerator enumerator = polyline3D.GetEnumerator();
        try
        {
          while (enumerator.MoveNext())
          {
            ObjectId current = (ObjectId) enumerator.Current;
            PolylineVertex3d polylineVertex3d = (PolylineVertex3d) transaction.GetObject(current, (OpenMode) 0);
            point3dList.Add(polylineVertex3d.get_Position());
          }
        }
        finally
        {
          (enumerator as IDisposable)?.Dispose();
        }
        for (int index = 0; index < point3dList.Count; ++index)
        {
          Polyline polyline2 = polyline1;
          int num = index;
          Point3d point3d = point3dList[index];
          double x = ((Point3d) ref point3d).get_X();
          point3d = point3dList[index];
          double y = ((Point3d) ref point3d).get_Y();
          Point2d point2d = new Point2d(x, y);
          polyline2.AddVertexAt(num, point2d, 0.0, 0.0, 0.0);
        }
        double num1 = ((Curve) polyline1).GetDistAtPoint(new Point3d(((Point3d) ref pt).get_X(), ((Point3d) ref pt).get_Y(), 0.0)) + distanceInput;
        if (num1 < 0.0 || num1 > polyline1.get_Length())
        {
          int num2 = (int) MessageBox.Show("Vị trí của điểm bạn muốn đặt nằm ngoài đối tượng. ", "Lỗi");
        }
        else
        {
          Point3d pointAtDist = ((Curve) polyline1).GetPointAtDist(num1);
          Line line = new Line(((Point3d) ref pointAtDist).Add(new Vector3d(0.0, 0.0, 500000000.0)), ((Point3d) ref pointAtDist).Add(new Vector3d(0.0, 0.0, -500000000.0)));
          Point3dCollection point3dCollection = new Point3dCollection();
          ((Entity) line).IntersectWith((Entity) polyline3D, (Intersect) 2, point3dCollection, IntPtr.Zero, IntPtr.Zero);
          if ((uint) point3dCollection.get_Count() > 0U)
          {
            double num3 = ((Curve) polyline3D).GetDistAtPoint(point3dCollection.get_Item(0)) - ((Curve) polyline3D).GetDistAtPoint(pt);
            editor.WriteMessage("\nChiều dài thực:" + Math.Abs(num3).ToString());
            Clipboard.SetText(Math.Abs(num3).ToString());
            using (DBPoint dbPoint = new DBPoint(point3dCollection.get_Item(0)))
            {
              blockTableRecord.AppendEntity((Entity) dbPoint);
              transaction.AddNewlyCreatedDBObject((DBObject) dbPoint, true);
              database.set_Pdmode(34);
              database.set_Pdsize(300.0);
            }
          }
          else
          {
            int num4 = (int) MessageBox.Show("Vị trí của điểm bạn muốn đặt nằm ngoài đối tượng. ", "Lỗi");
          }
        }
        transaction.Commit();
      }
    }

    public static void DrawingSelection2()
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      try
      {
        Editor editor = mdiActiveDocument.get_Editor();
        Transaction transaction = mdiActiveDocument.get_Database().get_TransactionManager().StartTransaction();
        using (transaction)
        {
          List<ObjectId> solidType = Main.GetSolidType();
          StreamWriter streamWriter = new StreamWriter("C:\\ProgramData\\Export File STL\\countObject.txt");
          streamWriter.WriteLine(((IEnumerable<ObjectId>) solidType).Count<ObjectId>().ToString());
          streamWriter.Close();
          editor.SetImpliedSelection(solidType.ToArray());
          transaction.Commit();
        }
      }
      catch
      {
      }
    }

    public static void CMDGETLAYERNAME()
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      try
      {
        mdiActiveDocument.get_Editor();
        Database database = mdiActiveDocument.get_Database();
        using (Transaction transaction = (Transaction) database.get_TransactionManager().StartOpenCloseTransaction())
        {
          LayerTable layerTable = transaction.GetObject(database.get_LayerTableId(), (OpenMode) 0) as LayerTable;
          string str = "";
          using (SymbolTableEnumerator enumerator = ((SymbolTable) layerTable).GetEnumerator())
          {
            while (enumerator.MoveNext())
            {
              ObjectId current = enumerator.get_Current();
              LayerTableRecord layerTableRecord = transaction.GetObject(current, (OpenMode) 1) as LayerTableRecord;
              str = str + ((SymbolTableRecord) layerTableRecord).get_Name() + Environment.NewLine;
            }
          }
          if (!Directory.Exists("C:\\ProgramData\\Export File STL"))
            Directory.CreateDirectory("C:\\ProgramData\\Export File STL");
          StreamWriter streamWriter = new StreamWriter("C:\\ProgramData\\Export File STL\\layers.txt");
          streamWriter.WriteLine(str);
          streamWriter.Close();
        }
      }
      catch
      {
      }
    }

    public static void FaceNormal()
    {
      Document mdiActiveDocument = Application.get_DocumentManager().get_MdiActiveDocument();
      Database database = mdiActiveDocument.get_Database();
      Editor editor = mdiActiveDocument.get_Editor();
      PromptEntityOptions promptEntityOptions = new PromptEntityOptions("\nSelect a 3D solid: ");
      promptEntityOptions.SetRejectMessage("\nInvalid selection...");
      promptEntityOptions.AddAllowedClass(typeof (TinSurface), true);
      PromptEntityResult entity = editor.GetEntity(promptEntityOptions);
      if (((PromptResult) entity).get_Status() != 5100)
        return;
      using (Transaction transaction = database.get_TransactionManager().StartTransaction())
      {
        TinSurfaceTriangleCollection triangles = (transaction.GetObject(entity.get_ObjectId(), (OpenMode) 0) as TinSurface).get_Triangles();
        string str1 = "solid AutoCAD" + Environment.NewLine;
        using (IEnumerator<TinSurfaceTriangle> enumerator = triangles.GetEnumerator())
        {
          while (((IEnumerator) enumerator).MoveNext())
          {
            TinSurfaceTriangle current = enumerator.Current;
            List<Point3d> vertexs = new List<Point3d>();
            vertexs.Add(current.get_Vertex1().get_Location());
            vertexs.Add(current.get_Vertex2().get_Location());
            vertexs.Add(current.get_Vertex3().get_Location());
            str1 += Main.ExportSTLASCIITestV2(vertexs);
          }
        }
        string str2 = str1 + "endsolid AutoCAD";
        using (StreamWriter streamWriter = new StreamWriter("D:\\MyNewFile.stl", false))
          streamWriter.Write(str2);
        transaction.Commit();
      }
    }

    private static string ExportSTLASCIITestV2(List<Point3d> vertexs)
    {
      string str1 = "";
      Vector3d normal = ((PlanarEntity) new Plane(vertexs[0], vertexs[1], vertexs[2])).get_Normal();
      string str2 = "   facet normal " + ((Vector3d) ref normal).get_X().ToString("e7") + " " + ((Vector3d) ref normal).get_Y().ToString("e7") + " " + ((Vector3d) ref normal).get_Z().ToString("e7");
      string str3 = str1 + str2 + Environment.NewLine + "      outer loop" + Environment.NewLine;
      string[] strArray1 = new string[8];
      strArray1[0] = str3;
      strArray1[1] = "         vertex ";
      Point3d point3d1 = vertexs[0];
      strArray1[2] = ((Point3d) ref point3d1).get_X().ToString("e7");
      strArray1[3] = " ";
      Point3d point3d2 = vertexs[0];
      strArray1[4] = ((Point3d) ref point3d2).get_Y().ToString("e7");
      strArray1[5] = " ";
      Point3d point3d3 = vertexs[0];
      strArray1[6] = ((Point3d) ref point3d3).get_Z().ToString("e7");
      strArray1[7] = Environment.NewLine;
      string str4 = string.Concat(strArray1);
      string[] strArray2 = new string[8];
      strArray2[0] = str4;
      strArray2[1] = "         vertex ";
      Point3d point3d4 = vertexs[1];
      strArray2[2] = ((Point3d) ref point3d4).get_X().ToString("e7");
      strArray2[3] = " ";
      Point3d point3d5 = vertexs[1];
      strArray2[4] = ((Point3d) ref point3d5).get_Y().ToString("e7");
      strArray2[5] = " ";
      Point3d point3d6 = vertexs[1];
      strArray2[6] = ((Point3d) ref point3d6).get_Z().ToString("e7");
      strArray2[7] = Environment.NewLine;
      string str5 = string.Concat(strArray2);
      string[] strArray3 = new string[8];
      strArray3[0] = str5;
      strArray3[1] = "         vertex ";
      Point3d point3d7 = vertexs[2];
      strArray3[2] = ((Point3d) ref point3d7).get_X().ToString("e7");
      strArray3[3] = " ";
      Point3d point3d8 = vertexs[2];
      strArray3[4] = ((Point3d) ref point3d8).get_Y().ToString("e7");
      strArray3[5] = " ";
      Point3d point3d9 = vertexs[2];
      strArray3[6] = ((Point3d) ref point3d9).get_Z().ToString("e7");
      strArray3[7] = Environment.NewLine;
      return string.Concat(strArray3) + "      endloop" + Environment.NewLine + "   endfacet" + Environment.NewLine;
    }

    private static string ExportSTLASCIITest(List<Point3d> vertexs)
    {
      string str1 = "";
      vertexs = ((IEnumerable<Point3d>) vertexs).Distinct<Point3d>().ToList<Point3d>();
      vertexs = Main.SortPointsClockwise2(vertexs);
      Vector3d normal = ((PlanarEntity) new Plane(vertexs[0], vertexs[1], vertexs[2])).get_Normal();
      string str2 = "   facet normal " + ((Vector3d) ref normal).get_X().ToString("e7") + " " + ((Vector3d) ref normal).get_Y().ToString("e7") + " " + ((Vector3d) ref normal).get_Z().ToString("e7");
      if (vertexs.Count < 3)
      {
        string str3 = str1 + str2 + Environment.NewLine + "      outer loop" + Environment.NewLine;
        string[] strArray1 = new string[8];
        strArray1[0] = str3;
        strArray1[1] = "         vertex ";
        Point3d point3d1 = vertexs[0];
        strArray1[2] = ((Point3d) ref point3d1).get_X().ToString("e7");
        strArray1[3] = " ";
        point3d1 = vertexs[0];
        strArray1[4] = ((Point3d) ref point3d1).get_Y().ToString("e7");
        strArray1[5] = " ";
        point3d1 = vertexs[0];
        double num = ((Point3d) ref point3d1).get_Z();
        strArray1[6] = num.ToString("e7");
        strArray1[7] = Environment.NewLine;
        string str4 = string.Concat(strArray1);
        string[] strArray2 = new string[8];
        strArray2[0] = str4;
        strArray2[1] = "         vertex ";
        Point3d point3d2 = vertexs[1];
        num = ((Point3d) ref point3d2).get_X();
        strArray2[2] = num.ToString("e7");
        strArray2[3] = " ";
        Point3d point3d3 = vertexs[1];
        num = ((Point3d) ref point3d3).get_Y();
        strArray2[4] = num.ToString("e7");
        strArray2[5] = " ";
        Point3d point3d4 = vertexs[1];
        num = ((Point3d) ref point3d4).get_Z();
        strArray2[6] = num.ToString("e7");
        strArray2[7] = Environment.NewLine;
        string str5 = string.Concat(strArray2);
        string[] strArray3 = new string[8];
        strArray3[0] = str5;
        strArray3[1] = "         vertex ";
        Point3d point3d5 = vertexs[2];
        num = ((Point3d) ref point3d5).get_X();
        strArray3[2] = num.ToString("e7");
        strArray3[3] = " ";
        Point3d point3d6 = vertexs[2];
        num = ((Point3d) ref point3d6).get_Y();
        strArray3[4] = num.ToString("e7");
        strArray3[5] = " ";
        Point3d point3d7 = vertexs[2];
        num = ((Point3d) ref point3d7).get_Z();
        strArray3[6] = num.ToString("e7");
        strArray3[7] = Environment.NewLine;
        str1 = string.Concat(strArray3) + "      endloop" + Environment.NewLine + "   endfacet" + Environment.NewLine;
      }
      else
      {
        for (int index = 1; index < vertexs.Count && index + 1 < vertexs.Count; ++index)
        {
          string str3 = str1 + str2 + Environment.NewLine + "      outer loop" + Environment.NewLine;
          string[] strArray1 = new string[8];
          strArray1[0] = str3;
          strArray1[1] = "         vertex ";
          Point3d point3d1 = vertexs[0];
          double num = ((Point3d) ref point3d1).get_X();
          strArray1[2] = num.ToString("e7");
          strArray1[3] = " ";
          point3d1 = vertexs[0];
          num = ((Point3d) ref point3d1).get_Y();
          strArray1[4] = num.ToString("e7");
          strArray1[5] = " ";
          point3d1 = vertexs[0];
          num = ((Point3d) ref point3d1).get_Z();
          strArray1[6] = num.ToString("e7");
          strArray1[7] = Environment.NewLine;
          string str4 = string.Concat(strArray1);
          string[] strArray2 = new string[8];
          strArray2[0] = str4;
          strArray2[1] = "         vertex ";
          Point3d point3d2 = vertexs[index];
          num = ((Point3d) ref point3d2).get_X();
          strArray2[2] = num.ToString("e7");
          strArray2[3] = " ";
          Point3d point3d3 = vertexs[index];
          num = ((Point3d) ref point3d3).get_Y();
          strArray2[4] = num.ToString("e7");
          strArray2[5] = " ";
          Point3d point3d4 = vertexs[index];
          num = ((Point3d) ref point3d4).get_Z();
          strArray2[6] = num.ToString("e7");
          strArray2[7] = Environment.NewLine;
          string str5 = string.Concat(strArray2);
          string[] strArray3 = new string[8];
          strArray3[0] = str5;
          strArray3[1] = "         vertex ";
          Point3d point3d5 = vertexs[index + 1];
          num = ((Point3d) ref point3d5).get_X();
          strArray3[2] = num.ToString("e7");
          strArray3[3] = " ";
          point3d5 = vertexs[index + 1];
          num = ((Point3d) ref point3d5).get_Y();
          strArray3[4] = num.ToString("e7");
          strArray3[5] = " ";
          point3d5 = vertexs[index + 1];
          num = ((Point3d) ref point3d5).get_Z();
          strArray3[6] = num.ToString("e7");
          strArray3[7] = Environment.NewLine;
          str1 = string.Concat(strArray3) + "      endloop" + Environment.NewLine + "   endfacet" + Environment.NewLine;
        }
      }
      return str1;
    }

    private static List<Point3d> SortPointsClockwise2(List<Point3d> points)
    {
      List<Point3d> point3dList1 = new List<Point3d>();
      Vector3d normal = ((PlanarEntity) new Plane(points[0], points[1], points[2])).get_Normal();
      Point3d arvagePoint = new Point3d(((IEnumerable<Point3d>) points).Average<Point3d>((Func<Point3d, double>) (t => ((Point3d) ref t).get_X())), ((IEnumerable<Point3d>) points).Average<Point3d>((Func<Point3d, double>) (t => ((Point3d) ref t).get_Y())), ((IEnumerable<Point3d>) points).Average<Point3d>((Func<Point3d, double>) (t => ((Point3d) ref t).get_Z())));
      Point3d orginPoint = points[0];
      List<Point3d> point3dList2 = Main.ConvertPoints(points[0], points[1], new Plane(points[0], normal), points, true);
      Point3d point1 = points[0];
      Point3d point2 = points[1];
      Plane plane = new Plane(points[0], normal);
      List<Point3d> points1 = new List<Point3d>();
      points1.Add(arvagePoint);
      arvagePoint = Main.ConvertPoints(point1, point2, plane, points1, true)[0];
      List<Point3d> list = ((IEnumerable<Point3d>) ((IEnumerable<Point3d>) point3dList2).OrderBy<Point3d, double>((Func<Point3d, double>) (t => Main.GetAngles(((Point3d) ref arvagePoint).GetVectorTo(orginPoint), ((Point3d) ref arvagePoint).GetVectorTo(t))))).ToList<Point3d>();
      return Main.ConvertPoints(points[0], points[1], new Plane(points[0], normal), list, false);
    }

    private static List<Point3d> ConvertPoints(
      Point3d OrginPoint,
      Point3d XPoint,
      Plane plane,
      List<Point3d> points,
      bool convert)
    {
      Database workingDatabase = HostApplicationServices.get_WorkingDatabase();
      Editor editor = Application.get_DocumentManager().get_MdiActiveDocument().get_Editor();
      List<Point3d> point3dList = new List<Point3d>();
      using (Transaction transaction = workingDatabase.get_TransactionManager().StartTransaction())
      {
        Vector3d vector3d1 = Point3d.op_Subtraction(XPoint, OrginPoint);
        Vector3d normal1 = ((Vector3d) ref vector3d1).GetNormal();
        Vector3d vector3d2 = ((Vector3d) ref normal1).RotateBy(Math.PI / 2.0, ((PlanarEntity) plane).get_Normal());
        Vector3d normal2 = ((Vector3d) ref vector3d2).GetNormal();
        Matrix3d coordinateSystem1 = editor.get_CurrentUserCoordinateSystem();
        ((Matrix3d) ref coordinateSystem1).get_CoordinateSystem3d();
        CoordinateSystem3d coordinateSystem3d;
        ((CoordinateSystem3d) ref coordinateSystem3d).\u002Ector(OrginPoint, normal1, normal2);
        if (convert)
        {
          Matrix3d matrix3d1 = Matrix3d.AlignCoordinateSystem(Point3d.get_Origin(), Vector3d.get_XAxis(), Vector3d.get_YAxis(), Vector3d.get_ZAxis(), ((CoordinateSystem3d) ref coordinateSystem3d).get_Origin(), ((CoordinateSystem3d) ref coordinateSystem3d).get_Xaxis(), ((CoordinateSystem3d) ref coordinateSystem3d).get_Yaxis(), ((CoordinateSystem3d) ref coordinateSystem3d).get_Zaxis());
          Matrix3d matrix3d2 = ((Matrix3d) ref matrix3d1).Inverse();
          editor.set_CurrentUserCoordinateSystem(matrix3d1);
          using (List<Point3d>.Enumerator enumerator = points.GetEnumerator())
          {
            while (enumerator.MoveNext())
            {
              Point3d current = enumerator.Current;
              Point3d point3d = ((Point3d) ref current).TransformBy(matrix3d2);
              point3dList.Add(point3d);
            }
          }
        }
        else
        {
          Matrix3d coordinateSystem2 = editor.get_CurrentUserCoordinateSystem();
          using (List<Point3d>.Enumerator enumerator = points.GetEnumerator())
          {
            while (enumerator.MoveNext())
            {
              Point3d current = enumerator.Current;
              Point3d point3d = ((Point3d) ref current).TransformBy(coordinateSystem2);
              point3dList.Add(point3d);
            }
          }
          editor.set_CurrentUserCoordinateSystem(Matrix3d.get_Identity());
          editor.UpdateTiledViewportsInDatabase();
        }
        transaction.Commit();
      }
      return point3dList;
    }

    private static double GetAngles(Vector3d V1, Vector3d V2)
    {
      double x = ((Vector3d) ref V1).get_X() * ((Vector3d) ref V2).get_X() + ((Vector3d) ref V1).get_Y() * ((Vector3d) ref V2).get_Y();
      return Math.Atan2(((Vector3d) ref V1).get_X() * ((Vector3d) ref V2).get_Y() - ((Vector3d) ref V1).get_Y() * ((Vector3d) ref V2).get_X(), x);
    }

    public static List<ObjectId> GetSolidType()
    {
      Database database = Application.get_DocumentManager().get_MdiActiveDocument().get_Database();
      List<ObjectId> objectIdList = new List<ObjectId>();
      Transaction transaction = database.get_TransactionManager().StartTransaction();
      List<string> stringList = new List<string>();
      if (File.Exists("C:\\ProgramData\\Export File STL\\layer.txt"))
        stringList = ((IEnumerable<string>) File.ReadAllLines("C:\\ProgramData\\Export File STL\\layer.txt")).ToList<string>();
      using (transaction)
      {
        using (BlockTableRecordEnumerator enumerator = ((BlockTableRecord) transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), (OpenMode) 0)).GetEnumerator())
        {
          while (enumerator.MoveNext())
          {
            ObjectId current = enumerator.get_Current();
            try
            {
              Solid3d solid3d = transaction.GetObject(current, (OpenMode) 1) as Solid3d;
              if (DisposableWrapper.op_Inequality((DisposableWrapper) solid3d, (DisposableWrapper) null) && stringList.Contains(((Entity) solid3d).get_Layer()))
                objectIdList.Add(((DBObject) solid3d).get_ObjectId());
            }
            catch (InvalidCastException ex)
            {
            }
          }
        }
        transaction.Commit();
      }
      return objectIdList;
    }

    public static string TextInput(string message, string defValue = null)
    {
      Editor editor = Application.get_DocumentManager().get_MdiActiveDocument().get_Editor();
      try
      {
        PromptStringOptions promptStringOptions1 = new PromptStringOptions(message);
        promptStringOptions1.set_UseDefaultValue(true);
        promptStringOptions1.set_DefaultValue(defValue);
        promptStringOptions1.set_AllowSpaces(false);
        PromptStringOptions promptStringOptions2 = promptStringOptions1;
        PromptResult promptResult = editor.GetString(promptStringOptions2);
        if (promptResult.get_Status() != 5100)
          return (string) null;
        return promptResult.get_StringResult();
      }
      catch (Exception ex)
      {
        return (string) null;
      }
    }
  }
}
