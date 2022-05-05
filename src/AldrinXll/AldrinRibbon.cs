using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExcelDna.Integration.CustomUI;
using ExcelDna.Integration;
using System.Diagnostics;
using ZQFXLLObjects;
using ExcelDna.Utilities;

namespace ZAddIn
{

    [ComVisible(true)]
    public class AldrinRibbon : ExcelRibbon
    {
        public void Recalculate(IRibbonControl control1)
        {
            ExcelAsyncUtil.QueueAsMacro(
             delegate
             {
                 System.Collections.Hashtable arraysDone = new System.Collections.Hashtable();

                 ExcelReference originalSelection = XlCall.Excel(XlCall.xlfSelection) as ExcelReference;
                 ExcelReference activeCell = XlCall.Excel(XlCall.xlfActiveCell) as ExcelReference;

                 try
                 {
                     XLApp.ScreenUpdating = false;
                     for (int row = (int)originalSelection.RowFirst; row <= (int)originalSelection.RowLast; row++)
                     {
                         for (int col = (int)originalSelection.ColumnFirst; col <= (int)originalSelection.ColumnLast; col++)
                         {
                             ExcelReference cellRange = new ExcelReference(row, col);
                             bool isFormula = (bool)XlCall.Excel(XlCall.xlfGetCell, 48, cellRange);

                             if (!isFormula) continue;

                             bool isArray = (bool)XlCall.Excel(XlCall.xlfGetCell, 49, cellRange);

                             string formula = (string)XlCall.Excel(XlCall.xlfGetCell, 6, cellRange);
                             if (isArray)
                             {
                                 XlCall.Excel(XlCall.xlcSelect, cellRange);
                                 XlCall.Excel(XlCall.xlcSelectSpecial, 6);
                                 ExcelReference arraySelection = XlCall.Excel(XlCall.xlfSelection) as ExcelReference;
                             }
                             else
                             {
                                 string address = (string)XlCall.Excel(XlCall.xlfReftext, cellRange, true);
                                 dynamic xlApp = ExcelDnaUtil.Application;
                                 dynamic callerRange = xlApp.Range[address];
                                 callerRange.FormulaLocal = formula;
                             }
                         }
                     }
                 }
                 finally
                 {
                     XlCall.Excel(XlCall.xlcSelect, originalSelection, activeCell);
                     XLApp.ScreenUpdating = true;
                 }
             });
        }

        public void ObjectFormat(IRibbonControl control1)
        {
            ExcelAsyncUtil.QueueAsMacro(
                delegate
                {
                    bool isFormula = (bool)XlCall.Excel(XlCall.xlfGetCell, 48);

                    if (!isFormula) return;

                    var contents = XlCall.Excel(XlCall.xlfGetCell, 5);
                    
                    if (contents.ToString().ToLower().StartsWith("obj_") ){
                        XlCall.Excel(XlCall.xlcFontProperties,
                            Type.Missing, Type.Missing, Type.Missing,
                            Type.Missing, Type.Missing, Type.Missing,
                            Type.Missing, Type.Missing, Type.Missing,
                            3  /* red in the default palette */
                            );

                        XlCall.Excel(XlCall.xlcAlignment,3, Type.Missing, Type.Missing, Type.Missing, Type.Missing);

                        //XlCall.Excel(XlCall.xlcFormatNumber, ";;;[Red][OBJECT]");
                        //XlCall.Excel(XlCall.xlcFormatText, ";;;[Blue]@");
                    }
                    else
                    {
                        return;
                    }
                });
        }
        public void SayHello(IRibbonControl control1)
        {
            MessageBox.Show("Hello!");

        }

        public void LogDisplay(IRibbonControl control1)
        {
            ExcelDna.Logging.LogDisplay.Show();
        }

        public void ObjectExplorer(IRibbonControl control1)
        {
            ExcelAsyncUtil.QueueAsMacro(
             delegate
             {
                 ExcelReference current = XlCall.Excel(XlCall.xlfActiveCell) as ExcelReference;
                 var refB1 = new ExcelReference(current.RowFirst, current.ColumnFirst);
                 if (refB1.GetValue().Equals(ExcelDna.Integration.ExcelEmpty.Value)) return;

                 ObjectHandler.Explore(refB1.GetValue().ToString());
             });
        }


        public void RangeDoubleCAPI(IRibbonControl control1)
        {
            ExcelAsyncUtil.QueueAsMacro(
             delegate
             {
                 object[,] inValues;
                 object[,] outValues;

                 try
                 {
                     ExcelReference inRange = (ExcelReference)XlCall.Excel(XlCall.xlfInput, "Range to read: ", 8 /*type_num = 8 : Range  */, "Doubling function");
                     ExcelReference outRange = (ExcelReference)XlCall.Excel(XlCall.xlfInput, "Range to write: ", 8 /*type_num = 8 : Range  */, "Doubling function");

                     inValues = (object[,])inRange.GetValue();
                     outValues = new object[inValues.GetLength(0), inValues.GetLength(1)];

                     for (int i = 0; i < inValues.GetLength(0); i++)
                     {
                         for (int j = 0; j < inValues.GetLength(1); j++)
                         {
                             if (inValues[i, j] is double)
                                 outValues[i, j] = (double)inValues[i, j] * 2.0;
                             else
                                 outValues[i, j] = "!ERROR";
                         }
                     }
                     outRange.SetValue(outValues);
                 }
                 catch (Exception e)
                 {
                     MessageBox.Show(e.ToString());
                 }
             });
        }




    }

}
