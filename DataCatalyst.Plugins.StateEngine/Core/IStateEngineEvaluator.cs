namespace DataCatalyst.StateEngine.Core;

using System;
using DataCatalyst;
using DataCatalyst.Composition;
using DataCatalyst.StateEngine.Models;

public interface IStateEngineEvaluator {
    Ref<State> Evaluate<TReader>(BakedStateGroup group, float[] sensorValues, bool atTarget)
        where TReader : struct, StateEngineEvaluator.ISensorReader;
    Ref<State> Evaluate(BakedStateGroup group, Func<Ref<Sensor>, float> sensorValueProvider, bool atTarget);
}
