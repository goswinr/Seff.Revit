﻿namespace RevitFabulous.Revit

open Autodesk.Revit.UI
open Autodesk.Revit.DB
open Autodesk.Revit.Attributes
open System


[<Transaction(TransactionMode.Manual)>]
type StartEditorCommand() =

    interface IExternalCommand with
        member this.Execute(commandData: ExternalCommandData, message: byref<string>, elements: ElementSet): Result = 
            try
                let v = [1;2;3] |> Seq.map string |> String.concat " "


                TaskDialog.Show("Revit", "Hello Seff World" + v) |> ignore 


                //https://thebuildingcoder.typepad.com/blog/2018/11/revit-window-handle-and-parenting-an-add-in-form.html
                let winHandle = Diagnostics.Process.GetCurrentProcess().MainWindowHandle
                
                let seff = Seff.App.runEditorHosted(winHandle,"Revit")
                seff.Window.Show()

                Result.Succeeded

            with ex ->
                TaskDialog.Show("Seff", sprintf "%A" ex ) |> ignore 

                
                
                
                Result.Failed


type SeffAddin() = // don't rename !
    interface IExternalApplication with
        member this.OnStartup(app:UIControlledApplication) =
            
            // Method to add Tab and Panel 
            let tabId = "Seff"
            app.CreateRibbonTab(tabId)

            let thisAssemblyPath = Reflection.Assembly.GetExecutingAssembly().Location

     
            let button = new PushButtonData("Seff", "Open Fsharp Editor", thisAssemblyPath, "RevitFabulous.Revit.StartEditorCommand")
            button.ToolTip <- "This will open the Seff Editor Window"
            //let uriImage = new Uri("pack://application:,,,/RevitTemplate;component/Resources/code-small.png");
            //let largeImage = new System.Windows.Media.Imaging.BitmapImage(uriImage);
            //button.LargeImage <- largeImage


            let panel = app.CreateRibbonPanel(tabId,"Seff")
            
            panel.AddItem(button) |> ignore 


            Result.Succeeded
        
        member this.OnShutdown(app:UIControlledApplication) =           

            Result.Succeeded

            // System.Guid.NewGuid().ToString().ToUpper() // for FSI