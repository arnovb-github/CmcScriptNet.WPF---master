﻿using CmcScriptNet.FilterBuilder.Extensions;
using CmcScriptNet.FilterBuilder.Models;
using System.ComponentModel;
using Vovin.CmcLibNet.Database;

namespace CmcScriptNet.FilterBuilder.Helpers
{
    /// <summary>
    /// Constructs a filter.
    /// </summary>
    // I *think* it would be better to make this into some static filter factory
    // I also think this may be a prime candidate for DI
    // I DUNNO BUT IT IS VERY BAD THIS NEEDS FIXING
    internal class FilterConstructor
    {
        #region Fields
        private readonly FilterControlModel _model;
        private readonly ICursorFilter _filter;
        #endregion

        #region Constructors
        internal FilterConstructor(FilterControlModel model, FilterType filterType)
        {
            _model = model;
            _model.PropertyChanged += Model_PropertyChanged;
            _filter = CreateFilter(_model.CategoryName, filterType);
        }
        #endregion

        #region Methods
        private ICursorFilter CreateFilter(string categoryName, FilterType filterType)
        {
            using (ICommenceDatabase db = new CommenceDatabase())
            {
                using (ICommenceCursor cur = db.GetCursor(categoryName))
                {
                    switch (filterType)
                    {
                        case FilterType.Field:
                            return (CursorFilterTypeF)cur.Filters.Add(_model.ClauseNumber, filterType);
                        case FilterType.ConnectionToItem:
                            return (CursorFilterTypeCTI)cur.Filters.Add(_model.ClauseNumber, filterType);
                        case FilterType.ConnectionToCategoryField:
                            return (CursorFilterTypeCTCF)cur.Filters.Add(_model.ClauseNumber, filterType);
                        case FilterType.ConnectionToCategoryToItem:
                            return (CursorFilterTypeCTCTI)cur.Filters.Add(_model.ClauseNumber, filterType);
                    }
                }
            }
            return null;
        }

        private CursorFilterTypeF ConstructFFilter(ICursorFilter filter, FilterControlModel m)
        {
            var f = (CursorFilterTypeF)filter;
            f.ClauseNumber = m.ClauseNumber;
            f.MatchCase = m.MatchCase;
            f.Except = m.Except;
            f.FieldName = m.CurrentFieldListItem.FieldName;
            f.Qualifier = m.SelectedFilterQualifier;
            if (m.SelectedFilterQualifier == FilterQualifier.Between)
            {
                f.FilterBetweenStartValue = m.BetweenStart;
                f.FilterBetweenEndValue = m.BetweenEnd;
            }
            if (m.SelectedFilterQualifier.GetAttribute<FilterValuesAttribute>()?.Number == 1)
            {
                f.FieldValue = m.FieldValue;
            }
            return f;
        }

        private CursorFilterTypeCTI ConstructCTIFilter(ICursorFilter filter, FilterControlModel m)
        {
            var f = (CursorFilterTypeCTI)filter;
            f.ClauseNumber = m.ClauseNumber;
            f.Except = m.Except;
            f.ClarifySeparator = _model.CurrentConnectedItem?.ClarifySeparator;
            f.ClarifyValue = _model.CurrentConnectedItem?.ClarifyValue;
            f.Connection = m.CurrentFieldListItem.ConnectionName;
            f.Category = m.CurrentFieldListItem.ToCategory;
            f.Item = m.CurrentConnectedItem?.ItemName;
            return f;
        }

        private CursorFilterTypeCTCF ConstructCTCFFilter(ICursorFilter filter, FilterControlModel m)
        {
            var f = (CursorFilterTypeCTCF)filter;
            f.ClauseNumber = m.ClauseNumber;
            f.Except = m.Except;
            f.Connection = m.SelectedFieldListItem.ConnectionName;
            f.Category = m.SelectedFieldListItem.ToCategory;
            f.FieldName = m.CurrentFieldListItem.FieldName;
            f.Qualifier = m.SelectedFilterQualifier;
            if (m.SelectedFilterQualifier == FilterQualifier.Between)
            {
                f.FilterBetweenStartValue = m.BetweenStart;
                f.FilterBetweenEndValue = m.BetweenEnd;
            }
            if (m.SelectedFilterQualifier.GetAttribute<FilterValuesAttribute>()?.Number == 1)
            {
                f.FieldValue = m.FieldValue;
            }
            return f;
        }

        private CursorFilterTypeCTCTI ConstructCTCTIFilter(ICursorFilter filter, FilterControlModel m)
        {
            var f = (CursorFilterTypeCTCTI)filter;
            f.ClauseNumber = m.ClauseNumber;
            f.Except = m.Except;
            f.Connection = m.SelectedFieldListItem.ConnectionName;
            f.ClarifySeparator = _model.CurrentConnectedItem?.ClarifySeparator;
            f.ClarifyValue = _model.CurrentConnectedItem?.ClarifyValue;
            f.Category = m.SelectedFieldListItem.ToCategory;
            f.Connection2 = m.CurrentFieldListItem.ConnectionName;
            f.Category2 = m.CurrentFieldListItem.ToCategory;
            f.Item = m.CurrentConnectedItem?.ItemName;
            return f;
        }
        #endregion

        #region Event handlers
        // TODO this is very fishy.
        // In effect we now have a custom class that responds to UI changes
        // The underlying idea is that filters will update automagically,
        // at least I think that that is the general idea
        // I am pretty convinced now it is a BAD idea
        // we should leave this kind of model-changing stuff to the model
        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // do not update properties that we update ourselves
            // this code will not win prizes :)
            if (e.PropertyName.Equals(nameof(_model.CurrentFilter))
                || e.PropertyName.Equals(nameof(_model.IsValid))) { return; }

            // we want to update the filter according to its type
            switch (_filter)
            {
                case CursorFilterTypeF _filter:
                    _filter = ConstructFFilter(_filter, _model);
                    break;
                case CursorFilterTypeCTI _filter:
                    _filter = ConstructCTIFilter(_filter, _model);
                    break;
                case CursorFilterTypeCTCF _filter:
                    _filter = ConstructCTCFFilter(_filter, _model);
                    break;
                case CursorFilterTypeCTCTI _filter:
                    _filter = ConstructCTCTIFilter(_filter, _model);
                    break;
            }
            _model.CurrentFilter = _filter; // pass back the updated filter to the model. BAD IDEA
            FilterValidator fv = new FilterValidator(_filter);
            _model.IsValid = fv.Validate(); // ALSO BAD IDEA
            // here would be a place we can put in our FilterString builder so the
            // textbox would not have to pass a control to ConverterParam,
            // but instead just be bound to a property
        }
        #endregion
    }
}
