﻿using DrakiaXYZ.BigBrain.Internal;
using HarmonyLib;
using SIT.Core.Misc;
using SIT.Tarkov.Core;
using System;
using System.Collections;
using System.Reflection;

using AICoreLogicAgentClass = AICoreAgentClass<BotLogicDecision>;
using AILogicActionResultStruct = AICoreActionResultStruct<BotLogicDecision>;

namespace DrakiaXYZ.BigBrain.Patches
{
    /**
     * Patch the bot agent update method so we can trigger a Start() method on custom logic actions
     **/
    internal class BotAgentUpdatePatch : ModulePatch
    {
        private static FieldInfo _strategyField;
        private static FieldInfo _lastResultField;
        private static FieldInfo _logicInstanceDictField;
        private static FieldInfo _lazyGetterField;

        protected override MethodBase GetTargetMethod()
        {
            Type botAgentType = typeof(AICoreLogicAgentClass);

            _strategyField = ReflectionHelpers.GetFieldFromTypeByFieldType(botAgentType, typeof(AICoreStrategyClass<>));
            _lastResultField = ReflectionHelpers.GetFieldFromTypeByFieldType(botAgentType, typeof(AILogicActionResultStruct));
            _logicInstanceDictField = ReflectionHelpers.GetFieldFromTypeByFieldType(botAgentType, typeof(IDictionary));
            _lazyGetterField = ReflectionHelpers.GetFieldFromTypeByFieldType(botAgentType, typeof(Delegate));

            return AccessTools.Method(botAgentType, "Update");
        }

        [PatchPrefix]
        public static bool PatchPrefix(object __instance)
        {


                // Get values we'll use later
                var strategy = _strategyField.GetValue(__instance) as AICoreStrategyClass<BotLogicDecision>;
                var aiCoreNodeDict = _logicInstanceDictField.GetValue(__instance) as IDictionary;

                // Update the brain, this is instead of method_10 in the original code
                strategy.ManualUpdate();

                // Call the brain update
                AILogicActionResultStruct lastResult = (AILogicActionResultStruct)_lastResultField.GetValue(__instance);
                AILogicActionResultStruct? result = strategy.Update(lastResult);
                if (result != null)
                {
                    // If an instance of our action doesn't exist in our dict, add it
                    int action = (int)result.Value.Action;
                    BaseNodeClass nodeInstance = aiCoreNodeDict[(BotLogicDecision)action] as BaseNodeClass;
                    if (nodeInstance == null)
                    {
                        Delegate lazyGetter = _lazyGetterField.GetValue(__instance) as Delegate;
                        nodeInstance = lazyGetter.DynamicInvoke(new object[] { (BotLogicDecision)action }) as BaseNodeClass;

                        if (nodeInstance != null)
                        {
                            aiCoreNodeDict.Add((BotLogicDecision)action, nodeInstance);
                        }
                    }

                    if (nodeInstance != null)
                    {
                        // If we're switching to a new action, call Start() on the new logic
                        if (lastResult.Action != result.Value.Action && nodeInstance is CustomLogicWrapper customLogic)
                        {
                            customLogic.Start();
                        }

                        nodeInstance.Update();
                    }

                    _lastResultField.SetValue(__instance, result);
                }

                return false;


        }
    }
}
