// Git integration for Tomboy
//
// Copyright 2010 Holger Berndt <berndth@gmx.de>
//
// This file is licensed LGPL version 2.1, see COPYING in this
// source distribution.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Mono.Unix;
using Mono.Unix.Native;

using Tomboy;

namespace Tomboy.Git
{
    public class GitLink : DynamicNoteTag
    {
        static Gdk.Pixbuf icon = null;
        
        static Gdk.Pixbuf Icon
        {
            get {
                if(icon == null)
                    icon = GuiUtils.GetIcon(System.Reflection.Assembly.GetExecutingAssembly(), "git", 16);
                return icon;
            }
        }
        
        public GitLink() : base()
        {
        }

        public override void Initialize(string element_name)
        {
            base.Initialize(element_name);

            Underline = Pango.Underline.Single;
            Foreground = "blue";
            CanActivate = true;

            Image = Icon;
        }

		public string RepositoryPath
		{
			get {
				return (string) Attributes["repo-path"];
			}
			set {
				Attributes["repo-path"] = value;
			}
		}

		public string Treeish
		{
			get {
				return (string) Attributes["treeish"];
			}
			set {
				Attributes["treeish"] = value;
			}
		}
        
        protected override bool OnActivate(NoteEditor editor,
                                           Gtk.TextIter start,
                                           Gtk.TextIter end)
        {
			Process p = new Process();
			p.StartInfo.FileName = "gitg";
			p.StartInfo.Arguments = "--select " + Treeish;
			p.StartInfo.WorkingDirectory = RepositoryPath;
			p.StartInfo.UseShellExecute = false;

			try {
				p.Start();
			} catch(Exception ee) {
				string message = String.Format("Error running Gitg: {0}", ee.Message);
				Logger.Error(message);
				HIGMessageDialog dialog = new HIGMessageDialog(editor.Toplevel as Gtk.Window,
				                              Gtk.DialogFlags.DestroyWithParent,
				                              Gtk.MessageType.Info,
				                              Gtk.ButtonsType.Ok,
				                              Catalog.GetString("Cannot open Git repository browser"),
				                              message);
				dialog.Run();
				dialog.Destroy();
			}
            return true;
        }        
    }
    
    public class GitNoteAddin : NoteAddin
    {        
        static GitNoteAddin()
        {
        }
        
        public override void Initialize()
        {
            if(!Note.TagTable.IsDynamicTagRegistered("link:git"))
                Note.TagTable.RegisterDynamicTag("link:git", typeof(GitLink));
        }

        Gtk.TargetList TargetList
        {
            get {
                return Gtk.Drag.DestGetTargetList(Window.Editor);
            }
        }

        public override void Shutdown()
        {
            if(HasWindow)
                TargetList.Remove(Gdk.Atom.Intern("x-gitg/treeish-list", false));
        }

        public override void OnNoteOpened()
        {
            TargetList.Add(Gdk.Atom.Intern("x-gitg/treeish-list", false), 0, 51);
            Window.Editor.DragDataReceived += OnDragDataReceived;
        }

        [DllImport("libgobject-2.0.so.0")]
        static extern void g_signal_stop_emission_by_name(IntPtr raw, string name);

        [GLib.ConnectBefore]
        void OnDragDataReceived(object sender, Gtk.DragDataReceivedArgs args)
        {
            bool stop_emission = false;

            if(args.SelectionData.Length < 0)
                return;

            if(args.Info == 51) {
                Gtk.Drag.Finish(args.Context, true, false, args.Time);
                stop_emission = true;
                HandleDrops(args.X, args.Y, Encoding.UTF8.GetString(args.SelectionData.Data));
            }
            else {
                foreach(Gdk.Atom atom in args.Context.Targets) {
                    if(atom.Name == "x-gitg/treeish-list") {
                        Gtk.Drag.GetData(Window.Editor, args.Context, Gdk.Atom.Intern("x-gitg/treeish-list", false), args.Time);
                        Gdk.Drag.Status(args.Context, Gdk.DragAction.Link, args.Time);
                        stop_emission = true;
                    }
                }
            }
            
            if(stop_emission) 
                g_signal_stop_emission_by_name(Window.Editor.Handle, "drag-data-received");
        }

        void HandleDrops(int xx, int yy, string treeish_list_string)
        {
            string [] treeish_list = treeish_list_string.Split('\n');

            string repository_path = treeish_list[0];
            bool first = true;
            for(int ii = 1; ii < treeish_list.Length; ii++) {
                HandleDrop(xx, yy, first, repository_path, treeish_list[ii]);
                first = false;
            }
        }
        
        string GetLinkText(string repository_path, string treeish)
        {
            string retval = null;
            Process p = new Process();
			p.StartInfo.FileName = "git";
			p.StartInfo.Arguments = "log --oneline " + treeish + "^.." + treeish;
			p.StartInfo.WorkingDirectory = repository_path;
			p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            try {
                p.Start();
                retval = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
			} catch(Exception ee) {
				Logger.Error(String.Format("Error running git: {0}", ee.Message));
			}

            return retval;
        }

        void HandleDrop(int xx, int yy, bool first, string repository_path, string treeish)
        {
            // Place the cursor in the position where the uri was
            // dropped, adjusting x,y by the TextView's VisibleRect.
            Gdk.Rectangle rect = Window.Editor.VisibleRect;
            xx += rect.X;
            yy += rect.Y;
            Gtk.TextIter cursor = Window.Editor.GetIterAtLocation(xx, yy);
            Buffer.PlaceCursor(cursor);
            
            int start_offset;
                
            if(!first) {
                cursor = Buffer.GetIterAtMark(Buffer.InsertMark);

                if(cursor.LineOffset == 0)
                    Buffer.Insert(ref cursor, "\n");
                else
                    Buffer.Insert(ref cursor, ", ");
            }
            
            GitLink link_tag;
            link_tag = (GitLink) Note.TagTable.CreateDynamicTag("link:git");

            link_tag.RepositoryPath = repository_path;
            link_tag.Treeish = treeish;
            
            cursor = Buffer.GetIterAtMark(Buffer.InsertMark);
            start_offset = cursor.Offset;
            string linktext = GetLinkText(repository_path, treeish);
            if(linktext == null)
                linktext = treeish;
            Buffer.Insert(ref cursor, linktext);
            Gtk.TextIter start = Buffer.GetIterAtOffset(start_offset);
            Gtk.TextIter end = Buffer.GetIterAtMark(Buffer.InsertMark);
            Buffer.ApplyTag(link_tag, start, end);
        }

    } // GitNoteAddin
}
