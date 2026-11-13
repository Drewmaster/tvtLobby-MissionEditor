using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyFileVersion("1.0.5.0")]
[assembly: AssemblyInformationalVersion("1.0.5")]

namespace TvtLobby.MissionEditor
{
    public sealed class Session
    {
        public string Token { get; set; }
        public int Port { get; set; }
        public int Protocol { get; set; }
    }

    public sealed class GroupRow
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Prefab { get; set; }
        public string DisplayName
        {
            get { return !String.IsNullOrWhiteSpace(Name) ? Name : String.IsNullOrWhiteSpace(Prefab) ? "Unnamed group" : Path.GetFileNameWithoutExtension(Prefab.Replace('/', Path.DirectorySeparatorChar)) + " (unnamed)"; }
        }
        public string Callsign { get; set; }
        public string EngineFaction { get; set; }
        public bool Playable { get; set; }
        public bool Selected { get; set; }
    }

    public sealed class Reply
    {
        public bool Ok { get; set; }
        public int Protocol { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string WorldPath { get; set; }
        public string ViewRevision { get; set; }
        public List<GroupRow> Groups { get; set; }
    }

    // The official Workbench NET API framing: little-endian Int32 + UTF-8 byte strings.
    public sealed class WorkbenchClient
    {
        public const int EditorProtocol = 1;
        public const string CompatibilityError = "This Mission Editor ZIP is incompatible with the scripts loaded in Workbench. Open tvtLobby 1.0.5 with the matching editor ZIP, then reopen Mission Editor.";
        private readonly Session session;
        public WorkbenchClient(Session value) { session = value; }
        public Reply Execute(string operation, Reply view, GroupRow row)
        {
            var request = new Dictionary<string, object> {
                {"APIFunc", "tvtLobby_MissionEditorBridge"}, {"Token", session.Token},
                {"Operation", operation}, {"ExpectedWorld", view == null ? "" : view.WorldPath},
                {"ViewRevision", view == null ? "" : view.ViewRevision}, {"EntityKey", row == null ? "" : row.Key},
                {"ExpectedPlayable", row != null && row.Playable}, {"Playable", row != null && !row.Playable}
            };
            return new JavaScriptSerializer().Deserialize<Reply>(Call(session.Port, request));
        }

        public static string Call(int port, object request, int timeout = 4000)
        {
            var fields = request as IDictionary<string, object>;
            if (fields != null)
            {
                var versioned = new Dictionary<string, object>(fields);
                versioned["Protocol"] = EditorProtocol;
                request = versioned;
            }
            return EditorOperationRetry.Call(port, request, timeout, RawCall);
        }

        public static void VerifyCompatibility(Session session)
        {
            if (session.Protocol != EditorProtocol) throw new IOException(CompatibilityError);
            var request = new Dictionary<string, object> {
                {"APIFunc", "tvtLobby_MissionEditorBridge"}, {"Token", session.Token},
                {"Operation", "hello"}, {"Protocol", EditorProtocol}
            };
            var reply = new JavaScriptSerializer().Deserialize<Reply>(RawCall(session.Port, request, 4000));
            if (reply != null && reply.Code == "NO_SESSION") throw new IOException(reply.Message);
            if (reply == null || !reply.Ok || reply.Protocol != EditorProtocol)
                throw new IOException(reply != null && reply.Code == "INCOMPATIBLE_EDITOR" ? reply.Message : CompatibilityError);
        }

        internal static string RawCall(int port, object request, int timeout)
        {
            using (var client = new TcpClient())
            {
                try
                {
                    var connection = client.BeginConnect("127.0.0.1", port, null, null);
                    using (connection.AsyncWaitHandle)
                    {
                        if (!connection.AsyncWaitHandle.WaitOne(1500)) throw new IOException(ConnectionHelp(port));
                        client.EndConnect(connection);
                    }
                }
                catch (SocketException error) { throw new IOException(ConnectionHelp(port), error); }
                client.ReceiveTimeout = timeout; client.SendTimeout = timeout;
                using (var stream = client.GetStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    writer.Write(1);
                    WriteString(writer, "tvtLobby.MissionEditor"); WriteString(writer, "JsonRPC");
                    WriteString(writer, request as string ?? EditorJson.SerializeRequest(request)); writer.Flush();
                    string code, payload;
                    try { code = ReadString(reader); payload = ReadString(reader); }
                    catch(IOException error)
                    {
                        var socket=error.InnerException as SocketException;
                        if(socket!=null&&socket.SocketErrorCode==SocketError.TimedOut)
                            throw new IOException("Workbench accepted the connection but did not return a response within "+(timeout/1000)+" seconds. Finish any open Workbench dialog or Play session, check the Workbench log, then Refresh. Your drafts are kept.",error);
                        throw;
                    }
                    if (code != "Ok") throw new IOException("Workbench: " + code);
                    return payload;
                }
            }
        }

        private static string ConnectionHelp(int port)
        {
            return "Cannot connect to Workbench on port " + port + ". In the main Workbench window, open File > Options > General > Net API, enable Enabled and check Port. Click OK, then Refresh here.";
        }

        private static void WriteString(BinaryWriter writer, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes);
        }
        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 16 * 1024 * 1024) throw new IOException("Invalid Workbench response length.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException("Workbench disconnected before sending the complete response.");
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public sealed class GroupsWindow : Form
    {
        private readonly WorkbenchClient client;
        private readonly DataGridView groups = new DataGridView();
        private readonly Label world = new EditorLabel();
        private readonly Label status = new EditorLabel();
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel();
        private Reply view;
        private bool busy;

        public GroupsWindow(Session session)
        {
            client = new WorkbenchClient(session);
            Text = "tvtLobby | Groups";
            MinimumSize = new Size(840, 520); Size = new Size(1120, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(28, 32, 34); ForeColor = Color.Gainsboro;
            AutoScaleMode = AutoScaleMode.Dpi;
            var frame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20) };
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            world.Dock = DockStyle.Fill; world.Text = "Connecting to Workbench…"; world.AutoEllipsis = true;
            groups.Dock = DockStyle.Fill; groups.ReadOnly = true; groups.MultiSelect = false;
            groups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            groups.AllowUserToAddRows = false; groups.AllowUserToDeleteRows = false;
            groups.AllowUserToResizeRows = false; groups.RowHeadersVisible = false;
            groups.AutoGenerateColumns = false; groups.BackgroundColor = Color.FromArgb(21, 25, 27);
            groups.BorderStyle = BorderStyle.None; groups.EnableHeadersVisualStyles = false;
            groups.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(43, 48, 51);
            groups.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            groups.ColumnHeadersHeight = 36; groups.RowTemplate.Height = 34;
            groups.DefaultCellStyle.BackColor = Color.FromArgb(30, 35, 38);
            groups.DefaultCellStyle.ForeColor = Color.Gainsboro;
            groups.DefaultCellStyle.SelectionBackColor = Color.FromArgb(104, 83, 40);
            groups.DefaultCellStyle.SelectionForeColor = Color.White;
            groups.GridColor = Color.FromArgb(53, 58, 61);
            AddColumn("DisplayName", "World entity / prefab", 36); AddColumn("Callsign", "Callsign", 30);
            AddColumn("EngineFaction", "Faction", 18);
            groups.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Playable", HeaderText = "Playable", Width = 85 });
            groups.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "In scene", Width = 85 });
            actions.Dock = DockStyle.Fill; actions.AutoSize = true; actions.Padding = new Padding(0, 12, 0, 12); actions.WrapContents = true;
            AddAction("Refresh", "snapshot"); AddAction("Select in world", "select");
            AddAction("Toggle playable", "set_playable"); AddAction("Undo", "undo");
            AddAction("Redo", "redo"); AddAction("Save world", "save");
            var mapButton = new Button { Text = "Map editor", Tag = "map", AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(104,83,40) };
            MapWindow mapWindow = null;
            mapButton.Click += (sender, args) => { if (mapWindow == null || mapWindow.IsDisposed) { mapWindow = new MapWindow(session); mapWindow.Show(this); } else mapWindow.Activate(); };
            actions.Controls.Add(mapButton);
            status.Dock = DockStyle.Fill; status.Padding = new Padding(0, 7, 0, 0);
            status.Text = "Groups prototype: reads the open world; scene edits use Workbench Undo. Save is explicit.";
            frame.Controls.Add(world, 0, 0); frame.Controls.Add(groups, 0, 1);
            frame.Controls.Add(actions, 0, 2); frame.Controls.Add(status, 0, 3); Controls.Add(frame);
            Shown += async (sender, args) => { await Execute("snapshot"); };
            UpdateActions();
        }

        private void AddColumn(string property, string title, int weight)
        {
            groups.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = title,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = weight });
        }
        private void AddAction(string title, string operation)
        {
            var button = new Button { Text = title, Tag = operation, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat,
                Padding = new Padding(7, 0, 7, 0), Margin = new Padding(0, 0, 8, 0), BackColor = Color.FromArgb(45, 50, 53) };
            button.Click += async (sender, args) => await Execute(operation);
            actions.Controls.Add(button);
        }
        private void UpdateActions()
        {
            foreach (Control control in actions.Controls)
                control.Enabled = !busy && ((string)control.Tag == "snapshot" || view != null);
        }
        private async Task Execute(string operation)
        {
            if (busy) return;
            GroupRow selected = groups.CurrentRow == null ? null : groups.CurrentRow.DataBoundItem as GroupRow;
            if ((operation == "select" || operation == "set_playable") && selected == null) { status.Text = "Select a group first."; return; }
            if (operation != "snapshot" && view == null) { status.Text = "Refresh to reconnect first."; return; }
            busy = true; UpdateActions();
            if (view == null) { world.Text = "Connecting to Workbench…"; status.Text = "Checking connection…"; }
            try
            {
                Reply result = await Task.Run(() => client.Execute(operation, view, selected));
                if (IsDisposed) return;
                if (result == null) throw new IOException("Workbench returned an empty response.");
                if (!result.Ok)
                {
                    view = null; groups.DataSource = null;
                    world.Text = "Refresh required\nNo current mission data.";
                    status.Text = result.Message + " [" + result.Code + "]";
                    return;
                }
                view = result;
                world.Text = "GROUPS\n" + result.WorldPath + "\n" + result.Groups.Count + " groups • changes are read again when you return to this window";
                groups.DataSource = result.Groups;
                if (selected != null)
                    foreach (DataGridViewRow gridRow in groups.Rows)
                        if (((GroupRow)gridRow.DataBoundItem).Key == selected.Key) { groups.CurrentCell = gridRow.Cells[0]; break; }
                status.Text = String.IsNullOrEmpty(result.Message) ? "Connected. Use Refresh after a manual edit; Save world writes through Workbench." : result.Message;
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    view = null; groups.DataSource = null;
                    world.Text = "Not connected to Workbench";
                    status.Text = error.Message;
                }
            }
            finally { busy = false; if (!IsDisposed) UpdateActions(); }
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                string sessionPath;
                if (args.Length == 2 && args[0] == "--session") sessionPath = args[1];
                else if (args.Length == 0)
                {
                    sessionPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tvtLobby-mission-editor.session.json"));
                    if (!File.Exists(sessionPath))
                        throw new ArgumentException("Open a world in Workbench, then choose Plugins → tvtLobby: Mission Editor to start a session.");
                }
                else throw new ArgumentException("Open this application from Workbench → Plugins → tvtLobby: Mission Editor.");
                var session = new JavaScriptSerializer().Deserialize<Session>(File.ReadAllText(sessionPath));
                Guid token;
                if (session == null || !Guid.TryParse(session.Token, out token) || session.Port < 1 || session.Port > 65535)
                    throw new ArgumentException("Invalid editor session. Reopen the application from Workbench.");
                if (session.Protocol != WorkbenchClient.EditorProtocol)
                    throw new IOException(WorkbenchClient.CompatibilityError);
                bool created;
                using (var instance = new Mutex(true, "Local\\tvtLobby.MissionEditor." + token.ToString("N"), out created))
                {
                    if (!created) { MessageBox.Show("Mission Editor is already open. Switch to its window with Alt+Tab.", "tvtLobby Mission Editor"); return 0; }
                    try
                    {
                        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new EditorWindow(session)); return 0;
                    }
                    finally { instance.ReleaseMutex(); }
                }
            }
            catch (Exception error) { MessageBox.Show(error.Message, "tvtLobby Mission Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
}
