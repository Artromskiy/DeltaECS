using Delta.ECS.Generators.Consumer;

int dense = ConsumerProof.Run();
int structural = ConsumerProof.RunStructural();
int queries = ConsumerProof.RunGenericQueries();
int where = ConsumerProof.RunGeneratedWhere();
int runtimeGeneric = ConsumerProof.RunRuntimeGenericFunctor();
int genericEntityForms = ConsumerProof.RunRuntimeGenericFunctorEntityForms();
int genericComponentForms = ConsumerProof.RunRuntimeGenericFunctorComponentForms();
int genericContextForms = ConsumerProof.RunRuntimeGenericFunctorContextForms();
int unaryGenericBindings = ConsumerProof.RunUnaryGenericBindings();
int standardGenericConstraints = ConsumerProof.RunStandardGenericConstraints();

if (dense <= 0 || structural != 21 || queries != 1 || where != 1 || runtimeGeneric != 34 || genericEntityForms != 1 || genericComponentForms != 1 || genericContextForms != 1 || unaryGenericBindings != 1 || standardGenericConstraints != 1)
{
    throw new InvalidOperationException(
        $"Consumer proof failed: dense={dense}, structural={structural}, queries={queries}, where={where}, generic={runtimeGeneric}, genericEntityForms={genericEntityForms}, genericComponentForms={genericComponentForms}, genericContextForms={genericContextForms}, unaryGenericBindings={unaryGenericBindings}, standardGenericConstraints={standardGenericConstraints}.");
}

Console.WriteLine(
    $"Consumer proof passed: dense={dense}, structural={structural}, queries={queries}, where={where}, generic={runtimeGeneric}, genericEntityForms={genericEntityForms}, genericComponentForms={genericComponentForms}, genericContextForms={genericContextForms}, unaryGenericBindings={unaryGenericBindings}, standardGenericConstraints={standardGenericConstraints}.");
