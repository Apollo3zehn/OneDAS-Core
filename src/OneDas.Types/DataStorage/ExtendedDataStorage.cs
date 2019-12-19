﻿using System;

namespace OneDas.DataStorage
{
    public class ExtendedDataStorage<T> : ExtendedDataStorageBase where T : struct
    {
        #region "Constuctors"

        public ExtendedDataStorage(int elementCount) : base(typeof(T), elementCount)
        {
            //
        }

        // required for HdfDataLoader (Activator.CreateInstance) because it uses a non-typed 'Array'.
        public ExtendedDataStorage(T[] dataset, byte[] statusSet) : base(typeof(T), statusSet)
        {
            dataset.CopyTo(this.GetDataBuffer<T>());
        }

        public ExtendedDataStorage(Span<T> dataset, byte[] statusSet) : base(typeof(T), statusSet)
        {
            dataset.CopyTo(this.GetDataBuffer<T>());
        }

        #endregion

        #region "Methods"

        public override object GetValue(int index)
        {
            return this.GetDataBuffer<T>()[index];
        }

        public override double[] ApplyDatasetStatus()
        {
            return ExtendedDataStorageBase.ApplyDatasetStatus(this.GetDataBuffer<T>().ToArray(), this.GetStatusBuffer().ToArray());
        }

        public override ISimpleDataStorage ToSimpleDataStorage()
        {
            return new SimpleDataStorage(this.ApplyDatasetStatus());
        }

        #endregion
    }
}