﻿using SCide.WPF.Commence;
using SCide.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using Vovin.CmcLibNet.Database;

namespace SCide.WPF.Models
{
    public class CommenceModel : ICommenceModel
    {
        #region Event declarations
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Fields
        private const string TEMPLATES_FOLDER = "tmplts"; // subfolder holding detail forms, report views etc.
        private readonly ICommenceMonitor _monitor;
        private readonly IList<string> _tempFiles = new List<string>();
        private IList<string> _categories;
        private IList<string> _forms;
        private IList<ICommenceItem> _items;
        private string _name = "Commence is not running";
        private string _path;

        #endregion

        #region Constructors
        public CommenceModel()
        {
            _monitor = new CommenceMonitor();
            _monitor.CommenceProcessExited += Monitor_CommenceProcessExited;
            _monitor.CommenceProcessStarted += Monitor_CommenceProcessStarted;
            if (_monitor.CommenceIsRunning)
            {
                Task.Run(async () => await InitializeModelAsync().ConfigureAwait(false)); // no UI stuff here, so no need to switch back to UI thread
            }
            this.PropertyChanged += CommenceModel_PropertyChanged;
        }


        #endregion

        #region Event handlers
        // we could do this in the property itself,
        // but you're not supposed to kick off background tasks from there.
        // The other thing is that we want to await, which we cannot do from a property
        private async void CommenceModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectedForm):
                    if (SelectedForm == null)
                    {
                        Items = null;
                    }
                    else
                    {
                        Items = await Task.Run(() => GetItemNames(this.SelectedCategory, MaxItems));
                    }
                    break;
            }
        }

        private async void Monitor_CommenceProcessStarted(object sender, EventArgs e)
        {
            await InitializeModelAsync();
        }

        private void Monitor_CommenceProcessExited(object sender, EventArgs e)
        {
            Name = "Commence is not running";
            Path = string.Empty;
            Categories = null;
            IsRunning = false;
        }
        #endregion

        #region Properties
        internal static List<IDFFile> FormFiles { get; private set; }

        public IList<string> Categories
        {
            get
            {
                return _categories;
            }
            private set
            {
                _categories = value;
                OnPropertyChanged();
            }
        }

        private string _selectedCategory;
        public string SelectedCategory
        {
            get => this._selectedCategory;
            set
            {
                this._selectedCategory = value;
                OnPropertyChanged();
                this.Forms = null; // clear forms property
                this.SelectedForm = null; // clear selected form property
                GetFormNames(this.SelectedCategory); // repopulate Forms property for current category
                if (Forms?.Count == 1) // immediately select the form if there is only 1
                {
                    this.SelectedForm = Forms.First();
                }
            }
        }

        private IList<ICommenceItem> GetItemNames(string categoryName, int maxItems)
        {
            IList<ICommenceItem> items = new List<ICommenceItem>();

            if (string.IsNullOrEmpty(categoryName)) { return items; }

            if (!_monitor.CommenceIsRunning) { return items; }

            // get the items
            // for some reason sometimes errors are thrown,
            // probably due to some timing issue with DDE in Vovin.CmcLibNet
            // we do not care about them here, so we simply try/catch them out
            try
            {
                using (ICommenceDatabase db = new CommenceDatabase())
                {
                    // should we cache this?
                    // I think I'll leave it as-is since the metadata may change.
                    string nameField = db.GetNameField(categoryName); // may throw an error on released RCW references..why?
                    string clarifyField = db.GetClarifyField(categoryName);
                    string clarifySeparator = db.GetClarifySeparator(categoryName);
                    using (ICommenceCursor cur = db.GetCursor(categoryName))
                    {
                        cur.SetColumn(0, nameField);
                        if (!string.IsNullOrEmpty(clarifyField)) { cur.SetColumn(1, clarifyField); }
                        using (ICommenceQueryRowSet qrs = cur.GetQueryRowSet(maxItems))
                        {
                            for (int i = 0; i < qrs.RowCount; i++)
                            {
                                ICommenceItem ci = new CommenceItem
                                {
                                    ClarifyFieldName = clarifyField,
                                    ClarifySeparator = clarifySeparator
                                };
                                var row = qrs.GetRow(i);
                                ci.ItemName = row[0].ToString();
                                if (cur.ColumnCount > 1)
                                {
                                    ci.ClarifyValue = row[1].ToString();
                                }
                                items.Add(ci);
                            }
                        }
                    }
                }

            }
            catch { } // swallow all errors
            return items;
        }

        private int MaxItems => 50;

        public void OpenForm()
        {
            {
                if (!_monitor.CommenceIsRunning)
                {
                    return;
                }
                using (ICommenceDatabase db = new CommenceDatabase())
                {
                    try
                    {
                        var item = SelectedItem;
                        if (item == null) { return; }

                        if (!string.IsNullOrEmpty(item.ClarifyFieldName))
                        {
                            var cin = db.ClarifyItemNames();
                            db.ClarifyItemNames("true");
                            string itemName = '"'+ db.GetClarifiedItemName(item.ItemName, item.ClarifySeparator, item.ClarifyValue) + '"';
                            db.ShowItem(SelectedCategory, itemName, SelectedForm);
                            db.ClarifyItemNames(cin);
                        }
                        else
                        {
                            string itemName = '"' + item.ItemName + '"';
                            db.ShowItem(SelectedCategory, itemName, SelectedForm);
                        }
                    }
                    // this call can fail for any number of reasons, so swallow all errors
                    // most errors will occur in Commence without reporting any error
                    // such as forms that auto-close or show a different form or embedded characters...
                    catch { } 
                }
            }
        }

        private string _selectedForm;
        public string SelectedForm
        {
            get { return _selectedForm; }
            set
            {
                _selectedForm = value;
                OnPropertyChanged();
            }
        }

        private ICommenceItem _selectedItem;
        public ICommenceItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        private ICommenceScript _currentScript;
        public ICommenceScript SelectedScript
        {
            get => _currentScript;
            set
            {
                _currentScript = value;
                OnPropertyChanged();
            }
        }

        public IList<string> Forms
        {
            get
            {
                return _forms;
            }
            set
            {
                _forms = value;
                OnPropertyChanged();
            }
        }

        public IList<ICommenceItem> Items
        {
            get
            {
                return _items;
            }
            set
            {
                _items = value;
                OnPropertyChanged();
            }
        }

        public IList<string> Fields { get; private set; }

        public IList<string> Connections { get; private set; }

        public string Name
        {
            get
            {
                return _name;
            }
            private set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Path
        {
            get
            {
                return _path;
            }
            private set
            {
                _path = value;
                OnPropertyChanged();
            }
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get { return _isRunning; }
            private set
            {
                _isRunning = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Event raisers
        // [CallerMemberName] eliminates the need to supply the propertyName
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Methods

        public async Task InitializeModelAsync()
        {
            using (ICommenceDatabase db = new CommenceDatabase())
            {
                Name = db.Name;
                Path = db.Path;
                Categories = db.GetCategoryNames();
                IsRunning = true;
            }
            FormFiles = await GetDetailFormFilesAsync();
        }

        public bool CheckInFormScript(ICommenceScript cs)
        {
            using (ICommenceDatabase db = new CommenceDatabase())
            {
                if (!this.CanSave(cs.DatabasePath))
                {
                    db.Close();
                    throw new Vovin.CmcLibNet.CommenceDDEException("Unable to check in script in Commence");
                }
                return db.CheckInFormScript(cs.CategoryName, cs.FormName, cs.FilePath);
            }
        }

        public bool CanSave(string path)
        {
            if (!_monitor.CommenceIsRunning) { return false; }
            return path.Equals(this.Path);
        }

        public void GetFormNames(string categoryName)
        {
            if (!_monitor.CommenceIsRunning)
            {
                Forms = null;
                return;
            }
            if (string.IsNullOrEmpty(categoryName)) { return; }
            using (ICommenceDatabase db = new CommenceDatabase())
            {
                Forms = db.GetFormNames(categoryName);
            }
        }

        public string CheckOutFormScript(string categoryName, string formName)
        {
            if (!_monitor.CommenceIsRunning) { return string.Empty; }
            string path = System.IO.Path.GetTempFileName();
            TempFileTracker.Add(path);
            using (ICommenceDatabase db = new CommenceDatabase())
            {
                if (!db.CheckOutFormScript(categoryName, formName, path)) // tell commence to save form to textfile
                {
                    path = string.Empty;
                }
            }
            return path;
        }

        // returns list of detail form xml files
        // this list is used to identify the controls on it, something the Commence API does not provide.
        // we could try and run this method based on the requested category,
        // but reading them all at once is faster since we don't have to loop over them every time
        private async Task<List<IDFFile>> GetDetailFormFilesAsync()
        {
            List<IDFFile> retval = new List<IDFFile>();
            string[] parts = {this.Path, TEMPLATES_FOLDER };
            string path = System.IO.Path.Combine(parts);
            string[] files = Directory.GetFiles(path, "fm*.xml"); // returns empty array if no files
            foreach (string f in files)
            {
                using (XmlReader reader = XmlReader.Create(f, new XmlReaderSettings() { Async = true }))
                {
                    try
                    {
                        while (await reader.ReadAsync())
                        {
                            // Only detect start elements.
                            if (reader.IsStartElement())
                            {
                                // Get element name and switch on it.
                                switch (reader.Name.ToLower())
                                {
                                    case "form":
                                        IDFFile idf = new IDFFile
                                        {
                                            // we want to read attributes
                                            Name = reader.GetAttribute("Name"),
                                            Category = reader.GetAttribute("CategoryName"),
                                            FileName = f
                                        };
                                        retval.Add(idf);
                                        break;
                                } //switch
                            } //if
                        } // while
                    } // try
                    catch { }
                } // using
            } // foreach
            return retval;
        }

        public void Focus()
        {
            _monitor.Focus(Name);
        }
        #endregion
    }
}
