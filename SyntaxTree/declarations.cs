﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxTree {

    // the declaration of an object
    public class Decln : ExternalDeclaration {
        public Decln(DeclnSpecs decl_specs_, List<InitializationDeclarator> init_declrs_) {
            decl_specs = decl_specs_;
            inner_init_declrs = init_declrs_;
        }

        public readonly DeclnSpecs decl_specs;
        public IReadOnlyList<InitializationDeclarator> init_declrs {
            get { return inner_init_declrs; }
        }

        private readonly List<InitializationDeclarator> inner_init_declrs;

        public Tuple<AST.Env, List<Tuple<AST.Env, AST.Decln>>> GetDeclns(AST.Env env) {
            List<Tuple<AST.Env, AST.Decln>> declns = new List<Tuple<AST.Env, AST.Decln>>();

            Tuple<AST.Env, AST.Decln.SCS, AST.ExprType> r_specs = decl_specs.GetSCSType(env);
            env = r_specs.Item1;
            AST.Decln.SCS scs = r_specs.Item2;
            AST.ExprType base_type = r_specs.Item3;

            foreach (InitializationDeclarator init_declr in init_declrs) {
                Tuple<AST.Env, AST.ExprType, AST.Expr, string> r_declr = init_declr.GetInitDeclr(env, base_type);

                env = r_declr.Item1;
                AST.ExprType type = r_declr.Item2;
                AST.Expr init = r_declr.Item3;
                string name = r_declr.Item4;

                // TODO : [finished] add the newly declared object into the environment
                AST.Env.EntryKind loc;
                switch (scs) {
                case AST.Decln.SCS.AUTO:
                    if (env.IsGlobal()) {
                        loc = AST.Env.EntryKind.GLOBAL;
                    } else {
                        loc = AST.Env.EntryKind.STACK;
                    }
                    break;
                case AST.Decln.SCS.EXTERN:
                    loc = AST.Env.EntryKind.GLOBAL;
                    break;
                case AST.Decln.SCS.STATIC:
                    loc = AST.Env.EntryKind.GLOBAL;
                    break;
                case AST.Decln.SCS.TYPEDEF:
                    loc = AST.Env.EntryKind.TYPEDEF;
                    break;
                default:
                    Log.SemantError("scs error");
                    return null;
                }
                env = env.PushEntry(loc, name, type);

                declns.Add(new Tuple<AST.Env, AST.Decln>(env, new AST.Decln(name, scs, type, init)));
            }

            return new Tuple<AST.Env, List<Tuple<AST.Env, AST.Decln>>>(env, declns);
        }

        public override Tuple<AST.Env, List<Tuple<AST.Env, AST.ExternDecln>>> GetExternDecln(AST.Env env) {
            Tuple<AST.Env, List<Tuple<AST.Env, AST.Decln>>> r_declns = GetDeclns(env);
            env = r_declns.Item1;
            List<Tuple<AST.Env, AST.ExternDecln>> declns = new List<Tuple<AST.Env, AST.ExternDecln>>();
            foreach (Tuple<AST.Env, AST.Decln> decln in r_declns.Item2) {
                declns.Add(new Tuple<AST.Env, AST.ExternDecln>(decln.Item1, decln.Item2));
            }
            return new Tuple<AST.Env, List<Tuple<AST.Env, AST.ExternDecln>>>(env, declns);
        }

    }


	/// <summary>
	/// Declaration Specifiers
	/// 
	/// storage class specifiers
	/// type specifiers
	/// type qualifiers
	/// </summary>
    public class DeclnSpecs : PTNode {
        public DeclnSpecs(List<StorageClassSpec> _scs,
                                     List<TypeSpecifier> _typespecs,
                                     List<TypeQualifier> _typequals) {
            specs_scs = _scs;
            specs_typequals = _typequals;
            specs_typespecs = _typespecs;
        }

        public readonly List<StorageClassSpec> specs_scs;
        public readonly List<TypeSpecifier> specs_typespecs;
        public readonly List<TypeQualifier> specs_typequals;

        // DeclnSpecs.SemantDeclnSpecs(env) -> (env, scs, type)
        public Tuple<AST.Env, AST.Decln.SCS, AST.ExprType> GetSCSType(AST.Env env) {
            Tuple<AST.Env, AST.ExprType> r_type = GetExprType(env);
            env = r_type.Item1;
            AST.ExprType type = r_type.Item2;

            AST.Decln.SCS scs;
            switch (GetStorageClass()) {
            case StorageClassSpec.AUTO:
            case StorageClassSpec.NULL:
            case StorageClassSpec.REGISTER:
                scs = AST.Decln.SCS.AUTO;
                break;
            case StorageClassSpec.EXTERN:
                scs = AST.Decln.SCS.EXTERN;
                break;
            case StorageClassSpec.STATIC:
                scs = AST.Decln.SCS.STATIC;
                break;
            case StorageClassSpec.TYPEDEF:
                scs = AST.Decln.SCS.TYPEDEF;
                break;
            default:
                throw new InvalidOperationException("Error: invalid storage class");
            }

            return new Tuple<AST.Env, AST.Decln.SCS, AST.ExprType>(env, scs, type);
        }

        // Get Expression Type : env -> (env, type)
        // ========================================
        // 
        public Tuple<AST.Env, AST.ExprType> GetExprType(AST.Env env) {

            Boolean is_const = specs_typequals.Exists(qual => qual == TypeQualifier.CONST);
            Boolean is_volatile = specs_typequals.Exists(qual => qual == TypeQualifier.VOLATILE);

            // 1. if no type specifier => Int32
            if (specs_typespecs.Count == 0) {
                return new Tuple<AST.Env, AST.ExprType>(env, new AST.TLong(is_const, is_volatile));
            }

            // 2. now let's analyse type specs
            if (specs_typespecs.All(spec => spec.basic != TypeSpecifier.Kind.NULL)) {
                List<TypeSpecifier.Kind> basic_specs = specs_typespecs.ConvertAll(spec => spec.basic);
                switch (GetBasicType(basic_specs)) {
                case AST.ExprType.Kind.CHAR:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TChar(is_const, is_volatile));

                case AST.ExprType.Kind.UCHAR:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TUChar(is_const, is_volatile));

                case AST.ExprType.Kind.SHORT:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TShort(is_const, is_volatile));

                case AST.ExprType.Kind.USHORT:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TUShort(is_const, is_volatile));

                case AST.ExprType.Kind.LONG:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TLong(is_const, is_volatile));

                case AST.ExprType.Kind.ULONG:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TULong(is_const, is_volatile));

                case AST.ExprType.Kind.FLOAT:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TFloat(is_const, is_volatile));

                case AST.ExprType.Kind.DOUBLE:
                    return new Tuple<AST.Env, AST.ExprType>(env, new AST.TDouble(is_const, is_volatile));

                default:
                    throw new Exception("Error: can't match type specifier");
                }

            } else if (specs_typespecs.Count == 1) {
                // now we can only match for struct, union, function...
                return specs_typespecs[0].GetExprType(env, is_const, is_volatile);

            } else {
                throw new InvalidOperationException("Error: can't match type specifier");

            }
        }

        // IsTypeOf
        // ========
        // Used by the parser
        // 
        public bool IsTypedef() {
            return specs_scs.Exists(scs => scs == StorageClassSpec.TYPEDEF);
        }

        // GetStorageClass
        // ===============
        // Infer the storage class
        // 
        public StorageClassSpec GetStorageClass() {
            if (specs_scs.Count == 0) {
                return StorageClassSpec.NULL;
            } else if (specs_scs.Count == 1) {
                return specs_scs[0];
            } else {
                throw new InvalidOperationException("Error: multiple storage class specifiers.");
            }
        }

        // GetBasicType
        // ============
        // input: specs
        // output: EnumExprType
        // returns a type from a list of type specifiers
        // 
        private static AST.ExprType.Kind GetBasicType(List<TypeSpecifier.Kind> specs) {
            foreach (KeyValuePair<TypeSpecifier.Kind[], AST.ExprType.Kind> pair in bspecs2enumtype) {
                if (!Enumerable.Except(pair.Key, specs).Any()) {
                    return pair.Value;
                }
            }
            Log.SemantError("Error: can't match type specifiers");
            return AST.ExprType.Kind.ERROR;
        }

        //// MatchSpecs
        //// ============================
        //// input: specs, key
        //// private
        //// Test whether the basic type specs matches the key
        //// 
        //private static bool MatchSpecs(List<TypeSpecifier.Kind> lhs, List<TypeSpecifier.Kind> rhs) {
        //    return lhs.Count == rhs.Count && rhs.All(item => lhs.Contains(item));
        //}

        private static IReadOnlyDictionary<TypeSpecifier.Kind[], AST.ExprType.Kind> bspecs2enumtype = new Dictionary<TypeSpecifier.Kind[], AST.ExprType.Kind> {

            // void
            [new[] { TypeSpecifier.Kind.VOID }] = AST.ExprType.Kind.VOID,

            // char
            [new[] { TypeSpecifier.Kind.CHAR }] = AST.ExprType.Kind.CHAR,
            [new[] { TypeSpecifier.Kind.SIGNED, TypeSpecifier.Kind.CHAR }] = AST.ExprType.Kind.CHAR,

            // uchar
            [new[] { TypeSpecifier.Kind.UNSIGNED, TypeSpecifier.Kind.CHAR }] = AST.ExprType.Kind.UCHAR,

            // short
            [new[] { TypeSpecifier.Kind.SHORT }] = AST.ExprType.Kind.SHORT,
            [new[] { TypeSpecifier.Kind.SIGNED, TypeSpecifier.Kind.SHORT }] = AST.ExprType.Kind.SHORT,
            [new[] { TypeSpecifier.Kind.SHORT, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.SHORT,
            [new[] { TypeSpecifier.Kind.SIGNED, TypeSpecifier.Kind.SHORT, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.SHORT,

            // ushort
            [new[] { TypeSpecifier.Kind.UNSIGNED, TypeSpecifier.Kind.SHORT }] = AST.ExprType.Kind.USHORT,
            [new[] { TypeSpecifier.Kind.UNSIGNED, TypeSpecifier.Kind.SHORT, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.USHORT,

            // long
            [new[] { TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.LONG,
            [new[] { TypeSpecifier.Kind.SIGNED }] = AST.ExprType.Kind.LONG,
            [new[] { TypeSpecifier.Kind.SIGNED, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.LONG,
            [new[] { TypeSpecifier.Kind.LONG }] = AST.ExprType.Kind.LONG,
            [new[] { TypeSpecifier.Kind.SIGNED, TypeSpecifier.Kind.LONG }] = AST.ExprType.Kind.LONG,
            [new[] { TypeSpecifier.Kind.LONG, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.LONG,
            [new[] { TypeSpecifier.Kind.SIGNED, TypeSpecifier.Kind.LONG, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.LONG,

            // ulong
            [new[] { TypeSpecifier.Kind.UNSIGNED }] = AST.ExprType.Kind.ULONG,
            [new[] { TypeSpecifier.Kind.UNSIGNED, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.ULONG,
            [new[] { TypeSpecifier.Kind.UNSIGNED, TypeSpecifier.Kind.LONG }] = AST.ExprType.Kind.ULONG,
            [new[] { TypeSpecifier.Kind.UNSIGNED, TypeSpecifier.Kind.LONG, TypeSpecifier.Kind.INT }] = AST.ExprType.Kind.ULONG,

            // float
            [new[] { TypeSpecifier.Kind.FLOAT }] = AST.ExprType.Kind.FLOAT,

            // double
            [new[] { TypeSpecifier.Kind.DOUBLE }] = AST.ExprType.Kind.DOUBLE,
            [new[] { TypeSpecifier.Kind.LONG, TypeSpecifier.Kind.DOUBLE }] = AST.ExprType.Kind.DOUBLE,

        };

    }


    // InitDeclr
    // =========
    // initialization declarator: a normal declarator + an initialization expression
    // 
    public class InitializationDeclarator : PTNode {

        public InitializationDeclarator(Declr _declr, Expr _init) {
            if (_declr != null) {
                declr = _declr;
            } else {
                declr = new NullDeclarator();
            }

            if (_init != null) {
                init = _init;
            } else {
                init = new EmptyExpr();
            }
        }

        public Declr declr;
        public Expr init;


        // TODO : InitDeclr.GetInitDeclr(env, type) -> (env, type, expr) : change the type corresponding to init expression
        public Tuple<AST.Env, AST.ExprType, AST.Expr, string> GetInitDeclr(AST.Env env, AST.ExprType type) {
            AST.Expr ast_init = init.GetExpr(env);

            Tuple<AST.Env, AST.ExprType, string> r_declr = declr.WrapExprType(env, type);
            env = r_declr.Item1;
            type = r_declr.Item2;
            string name = r_declr.Item3;

            return new Tuple<AST.Env, AST.ExprType, AST.Expr, string>(env, type, ast_init, name);
        }

    }


    public enum StorageClassSpec {
        NULL,
        ERROR,
        AUTO,
        REGISTER,
        STATIC,
        EXTERN,
        TYPEDEF
    }

    // TypeSpec
    // ========
    // TypeSpec
    //    |
    //    +--- TypedefName
    //    |
    //    +--- EnumSpec
    //    |
    //    +--- StructOrUnionSpec
    //                 |
    //                 +--- StructSpec
    //                 |
    //                 +--- UnionSpec
    //
    public class TypeSpecifier : PTNode {
        public enum Kind {
            NULL,
            VOID,
            CHAR,
            SHORT,
            INT,
            LONG,
            FLOAT,
            DOUBLE,
            SIGNED,
            UNSIGNED
        }

        public TypeSpecifier() {
            basic = Kind.NULL;
        }
        public TypeSpecifier(Kind spec) {
            basic = spec;
        }

        // GetExprType
        // ===========
        // input: env
        // output: tuple<ExprType, Environment>
        // 
        public virtual Tuple<AST.Env, AST.ExprType> GetExprType(AST.Env env, Boolean is_const, Boolean is_volatile) {
            throw new NotImplementedException();
        }

        public readonly Kind basic;
    }


    /// <summary>
    /// Typedef Name
	/// 
	/// Represents a name that has been previously defined as a typedef.
    /// </summary>
    public class TypedefName : TypeSpecifier {
        public TypedefName(string _name) {
            name = _name;
        }

        public override Tuple<AST.Env, AST.ExprType> GetExprType(AST.Env env, Boolean is_const, Boolean is_volatile) {
			AST.Env.Entry r_find = env.Find(name);
			if (r_find.kind == AST.Env.EntryKind.NOT_FOUND) {
				throw new InvalidOperationException("Error: cannot find name \"" + name + "\"");
			}

			if (r_find.kind != AST.Env.EntryKind.TYPEDEF) {
				throw new InvalidOperationException("Error: \"" + name + "\" is not a typedef name");
			}

			return Tuple.Create(env, r_find.type.GetQualifiedType(is_const, is_volatile));
        }


        public readonly string name;
    }


    public enum TypeQualifier {
        NULL,
        CONST,
        VOLATILE
    }



    // Type Modifier
    // =============
    // Modify a type into a function, array, or pointer
    // 
    public abstract class TypeModifier : PTNode {
        public enum TypeModifierKind {
            FUNCTION,
            ARRAY,
            POINTER
        }

        public TypeModifier(TypeModifierKind _kind) {
            modifier_kind = _kind;
        }

        // Modify Type : (env, type) -> (env, type)
        // ========================================
        // 
        public abstract Tuple<AST.Env, AST.ExprType> ModifyType(AST.Env env, AST.ExprType type);

        public readonly TypeModifierKind modifier_kind;
    }

    public class FunctionModifier : TypeModifier {
        public FunctionModifier(ParameterTypeList _param_type_list)
            : base(TypeModifierKind.FUNCTION) {
            param_type_list = _param_type_list;
        }
        public ParameterTypeList param_type_list;

        // Modify Type : (env, type) -> (env, type)
        // ========================================
        // 
        public override Tuple<AST.Env, AST.ExprType> ModifyType(AST.Env env, AST.ExprType ret_type) {
            Tuple<Boolean, List<Tuple<AST.Env, string, AST.ExprType>>> r_params = param_type_list.GetParamTypes(env);
            Boolean varargs = r_params.Item1;
            List<Tuple<AST.Env, string, AST.ExprType>> param_types = r_params.Item2;

            List<Tuple<string, AST.ExprType>> args = param_types.ConvertAll(arg => {
                env = arg.Item1;
                return Tuple.Create(arg.Item2, arg.Item3);
            });

            return new Tuple<AST.Env, AST.ExprType>(env, AST.TFunction.Create(ret_type, args, varargs));
        }
    }

    public class ArrayModifier : TypeModifier {
        public ArrayModifier(Expr _nelems)
            : base(TypeModifierKind.ARRAY) {
            array_nelems = _nelems;
        }

        // Modify Type : (env, type) => (env, type)
        // ========================================
        // 
        public override Tuple<AST.Env, AST.ExprType> ModifyType(AST.Env env, AST.ExprType type) {
            AST.Expr expr_nelems = array_nelems.GetExpr(env);

            // Try to cast the 'nelems' expression to a long int.
            expr_nelems = AST.TypeCast.MakeCast(expr_nelems, new AST.TLong());

            if (!expr_nelems.IsConstExpr()) {
                throw new InvalidOperationException("Error: size of the array is not a constant.");
            }

            Int32 nelems = ((AST.ConstLong)expr_nelems).value;
            return new Tuple<AST.Env, AST.ExprType>(env, new AST.TArray(type, nelems));
        }

        public readonly Expr array_nelems;
    }

    public class PointerModifier : TypeModifier {
        public PointerModifier(List<TypeQualifier> _type_qualifiers)
            : base(TypeModifierKind.POINTER) {
            type_qualifiers = _type_qualifiers;
        }

        // Modify Type : (env, type) => (env, type)
        // ========================================
        // 
        public override Tuple<AST.Env, AST.ExprType> ModifyType(AST.Env env, AST.ExprType type) {
            Boolean is_const = type_qualifiers.Any(x => x == TypeQualifier.CONST);
            Boolean is_volatile = type_qualifiers.Any(x => x == TypeQualifier.VOLATILE);
            return new Tuple<AST.Env, AST.ExprType>(env, new AST.TPointer(type, is_const, is_volatile));
        }
        public readonly List<TypeQualifier> type_qualifiers;
    }

    public class Declr : PTNode {
        public Declr(string _name, List<TypeModifier> _declr_modifiers) {
            inner_declr_modifiers = _declr_modifiers;
            declr_name = _name;
        }

        public Declr()
            : this("", new List<TypeModifier>()) { }

        public IReadOnlyList<TypeModifier> declr_modifiers {
            get { return inner_declr_modifiers; }
        }
        private readonly List<TypeModifier> inner_declr_modifiers;
        public readonly string declr_name;

        // TODO : [finished] Declr.WrapExprType(env, type) -> (env, type, name) : wrap up the type
        public virtual Tuple<AST.Env, AST.ExprType, string> WrapExprType(AST.Env env, AST.ExprType type) {
            for (int i = inner_declr_modifiers.Count; i --> 0;) {
                TypeModifier modifier = inner_declr_modifiers[i];

                Tuple<AST.Env, AST.ExprType> r = modifier.ModifyType(env, type);
                env = r.Item1;
                type = r.Item2;
            }
            return new Tuple<AST.Env, AST.ExprType, string>(env, type, declr_name);
        }
    }

    public class NullDeclarator : Declr {
        public NullDeclarator() : base("", new List<TypeModifier>()) { }

        public override Tuple<AST.Env, AST.ExprType, string> WrapExprType(AST.Env env, AST.ExprType type) {
            return new Tuple<AST.Env, AST.ExprType, string>(env, type, "");
        }
    }

    // Parameter Type List
    // ===================
    // 
    public class ParameterTypeList : PTNode {
        public ParameterTypeList(List<ParameterDeclaration> _param_list, Boolean _varargs) {
            params_varargs = _varargs;
            params_inner_declns = _param_list;
        }

        public ParameterTypeList(List<ParameterDeclaration> _param_list)
            : this(_param_list, false) { }

        public readonly Boolean params_varargs;
        public IReadOnlyList<ParameterDeclaration> params_declns {
            get { return params_inner_declns; }
        }
        public readonly List<ParameterDeclaration> params_inner_declns;

        // Get Parameter Types
        // ===================
        // 
        public Tuple<Boolean, List<Tuple<AST.Env, string, AST.ExprType>>> GetParamTypes(AST.Env env) {
            return Tuple.Create(
                params_varargs,
                params_inner_declns.ConvertAll(decln => {
                    Tuple<AST.Env, string, AST.ExprType> r_decln = decln.GetParamDecln(env);
                    env = r_decln.Item1;
                    return r_decln;
                })
            );
        }

    }


	/// <summary>
	/// Enum Specifier
	/// 
	/// enum enum-name {
	///     ENUM-0,
	///     ENUM-1,
	/// 	...
	/// }
	/// </summary>
    public class EnumSpecifier : TypeSpecifier {
        public EnumSpecifier(string _name, List<Enumerator> _enum_list) {
            spec_name = _name;
            spec_enums = _enum_list;
        }

        public override Tuple<AST.Env, AST.ExprType> GetExprType(AST.Env env, Boolean is_const, Boolean is_volatile) {
            if (spec_enums == null) {
                // if there is no content in this enum type, we must find it's definition in the environment
                AST.Env.Entry entry = env.Find("enum " + spec_name);
                if (entry == null || entry.kind != AST.Env.EntryKind.TYPEDEF) {
                    Log.SemantError("Error: type 'enum " + spec_name + " ' has not been defined.");
                    return null;
                }
            } else {
                // so there are something in this enum type, we need to put this type into the environment
                Int32 idx = 0;
                foreach (Enumerator elem in spec_enums) {
					Tuple<AST.Env, string, Int32> r_enum = elem.GetEnumerator(env, idx);
					env = r_enum.Item1;
					string name = r_enum.Item2;
					idx = r_enum.Item3;
					env = env.PushEnum(name, new AST.TLong(), idx);
                    idx++;
                }
                env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "enum " + spec_name, new AST.TLong());
            }

            return new Tuple<AST.Env, AST.ExprType>(env, new AST.TLong(is_const, is_volatile));
        }

        public readonly string spec_name;
        public readonly List<Enumerator> spec_enums;

    }


    public class Enumerator : PTNode {
        public Enumerator(string _name, Expr _init) {
            enum_name = _name;
            enum_init = _init;
        }
        public readonly string enum_name;
		public readonly Expr enum_init;

		public Tuple<AST.Env, string, Int32> GetEnumerator(AST.Env env, Int32 idx) {
			AST.Expr init;

			if (enum_init == null) {
				return new Tuple<AST.Env, string, int>(env, enum_name, idx);
			}

			init = enum_init.GetExpr(env);

            init = AST.TypeCast.MakeCast(init, new AST.TLong());
			if (!init.IsConstExpr()) {
				throw new InvalidOperationException("Error: expected constant integer");
			}
			Int32 init_idx = ((AST.ConstLong)init).value;

			return new Tuple<AST.Env, string, int>(env, enum_name, init_idx);
		}
    }


    // StructOrUnionSpec
    // =================
    // a base class of StructSpec and UnionSpec
    // not present in the semant phase
    // 
    public abstract class StructOrUnionSpecifier : TypeSpecifier {
		public StructOrUnionSpecifier(string _name, List<StructDeclaration> _declns) {
			name = _name;
			declns = _declns;
		}
        public readonly string name;
        public readonly List<StructDeclaration> declns;
    }


	/// <summary>
	/// Struct Specifier
	/// 
	/// Specifies a struct type.
	/// 
	/// if name == "", then
	///     the parser ensures that declns != null,
	///     and this specifier does not change the environment
	/// if name != "", then
	///     if declns == null
	///        this means that this specifier is just mentioning a struct, not defining one, so
	///        if the current environment doesn't have this struct type, then add an **incomplete** struct
	///     if declns != null
	///        this means that this specifier is defining a struct, so we need to perform the following steps:
	///        1. make sure that the current environment doesn't have a **complete** struct of this name
	///        2. immediately add an **incomplete** struct into the environment
 	///        3. iterate over the declns
	///        4. finish forming a complete struct and add it into the environment
	/// </summary>
    public class StructSpecifier : StructOrUnionSpecifier {
        public StructSpecifier(string _name, List<StructDeclaration> _declns)
			: base(_name, _declns) { }

		public Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> GetAttribs(AST.Env env) {
			List<Tuple<string, AST.ExprType>> attribs = new List<Tuple<string, AST.ExprType>>();
			foreach (StructDeclaration decln in declns) {
				Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> r_decln = decln.GetDeclns(env);
				env = r_decln.Item1;
				attribs.AddRange(r_decln.Item2);
			}
			return Tuple.Create(env, attribs);
		}

        public override Tuple<AST.Env, AST.ExprType> GetExprType(AST.Env env, Boolean is_const, Boolean is_volatile) {

			if (name == "") {
				// if no name supplied

				if (declns == null) {
					throw new ArgumentNullException("Error: parser should ensure declns != null");
				}

				Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> r_attribs = GetAttribs(env);
				env = r_attribs.Item1;

				return new Tuple<AST.Env, AST.ExprType>(env, AST.TStruct.Create(r_attribs.Item2, is_const, is_volatile));

			} else {
				// name supplied

				if (declns == null) {
					// if no declns supplied, then we are mentioning a struct

					AST.Env.Entry r_find = env.Find("struct " + name);

					// if the struct is not in the current environment
					if (r_find.kind == AST.Env.EntryKind.NOT_FOUND) {

						// add an incomplete struct into the environment
						AST.TIncompleteStruct incomplete_type = new AST.TIncompleteStruct(name, is_const, is_volatile);
						env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "struct " + name, incomplete_type);

						return new Tuple<AST.Env, AST.ExprType>(env, incomplete_type);
					}

					if (r_find.kind != AST.Env.EntryKind.TYPEDEF) {
						throw new InvalidOperationException("Error: find struct " + name + " not a type. This should be my fault.");
					}

					return Tuple.Create(env, r_find.type);

				} else {
					// declns supplied

					// 1. make sure there is no complete struct in the current environment
					if (env.Find("struct " + name).type.kind == AST.ExprType.Kind.STRUCT) {
						throw new InvalidOperationException("Error: re-defining a struct");
					}

					// 2. add an incomplete struct into the environment
					AST.TIncompleteStruct incomplete_type = new AST.TIncompleteStruct(name, is_const, is_volatile);
					env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "struct " + name, incomplete_type);


					// 3. iterate over the attribs
					Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> r_attribs = GetAttribs(env);
					env = r_attribs.Item1;

					// 4. create the type
					AST.TStruct type = AST.TStruct.Create(r_attribs.Item2, is_const, is_volatile);

					// 5. add into the environment
					env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "struct " + name, type);

					return new Tuple<AST.Env, AST.ExprType>(env, type);

				}


			}

        }

    }


    // UnionSpec
    // =========
    // 
    public class UnionSpecifier : StructOrUnionSpecifier {
        public UnionSpecifier(string _name, List<StructDeclaration> _declns)
			: base(_name, _declns) { }

		public Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> GetAttribs(AST.Env env) {
			List<Tuple<string, AST.ExprType>> attribs = new List<Tuple<string, AST.ExprType>>();
			foreach (StructDeclaration decln in declns) {
				Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> r_decln = decln.GetDeclns(env);
				env = r_decln.Item1;
				attribs.AddRange(r_decln.Item2);
			}
			return Tuple.Create(env, attribs);
		}

        // GetExprType
        // ===========
        // input: env, is_const, is_volatile
        // output: tuple<ExprType, Environment>
        // 
        // TODO : UnionSpec.GetExprType(env, is_const, is_volatile) -> (type, env)
        // 
        public override Tuple<AST.Env, AST.ExprType> GetExprType(AST.Env env, Boolean is_const, Boolean is_volatile) {
            
			if (name == "") {
				// if no name supplied

				if (declns == null) {
					throw new ArgumentNullException("Error: parser should ensure declns != null");
				}

				Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> r_attribs = GetAttribs(env);
				env = r_attribs.Item1;

				return new Tuple<AST.Env, AST.ExprType>(env, AST.TUnion.Create(r_attribs.Item2, is_const, is_volatile));

			} else {
				// name supplied

				if (declns == null) {
					// if no declns supplied, then we are mentioning a union

					AST.Env.Entry r_find = env.Find("union " + name);

					// if the struct is not in the current environment
					if (r_find.kind == AST.Env.EntryKind.NOT_FOUND) {

						// add an incomplete union into the environment
						AST.TIncompleteUnion incomplete_type = new AST.TIncompleteUnion(name, is_const, is_volatile);
						env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "union " + name, incomplete_type);

						return new Tuple<AST.Env, AST.ExprType>(env, incomplete_type);
					}

					if (r_find.kind != AST.Env.EntryKind.TYPEDEF) {
						throw new InvalidOperationException("Error: find union " + name + " not a type. This should be my fault.");
					}

					return Tuple.Create(env, r_find.type);

				} else {
					// declns supplied

					// 1. make sure there is no complete struct in the current environment
					if (env.Find("union " + name).type.kind == AST.ExprType.Kind.UNION) {
						throw new InvalidOperationException("Error: re-defining a union");
					}

					// 2. add an incomplete struct into the environment
					AST.TIncompleteUnion incomplete_type = new AST.TIncompleteUnion(name, is_const, is_volatile);
					env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "union " + name, incomplete_type);


					// 3. iterate over the attribs
					Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> r_attribs = GetAttribs(env);
					env = r_attribs.Item1;

					// 4. create the type
					AST.TUnion type = AST.TUnion.Create(r_attribs.Item2, is_const, is_volatile);

					// 5. add into the environment
					env = env.PushEntry(AST.Env.EntryKind.TYPEDEF, "union " + name, type);

					return new Tuple<AST.Env, AST.ExprType>(env, type);

				}
			}
        }

    }


    // StructOrUnion
    // =============
    // only used in parsing phase
    // 
    public class StructOrUnion : PTNode {
        public StructOrUnion(Boolean _is_union) {
            is_union = _is_union;
        }
        public Boolean is_union;
    }


    public class StructDeclaration : PTNode {
        public StructDeclaration(DeclnSpecs _specs, List<Declr> _declrs) {
            specs = _specs;
            declrs = _declrs;
        }
		public readonly DeclnSpecs specs;
        public readonly List<Declr> declrs;

        // Get Declarations : env -> (env, (name, type)[])
        // ===============================================
        // 
        public Tuple<AST.Env, List<Tuple<string, AST.ExprType>>> GetDeclns(AST.Env env) {
            Tuple<AST.Env, AST.ExprType> r_specs = specs.GetExprType(env);
            env = r_specs.Item1;
            AST.ExprType base_type = r_specs.Item2;

            List<Tuple<string, AST.ExprType>> attribs = new List<Tuple<string, AST.ExprType>>();
            foreach (Declr declr in declrs) {
                Tuple<AST.Env, AST.ExprType, string> r_declr = declr.WrapExprType(env, base_type);
                env = r_declr.Item1;
                AST.ExprType type = r_declr.Item2;
                string name = r_declr.Item3;
                attribs.Add(new Tuple<string, AST.ExprType>(name, type));
            }
            return new Tuple<AST.Env, List<Tuple<string, AST.ExprType>>>(env, attribs);
        }

    }

    // Parameter Declaration
    // =====================
    // 
    public class ParameterDeclaration : PTNode {
        public ParameterDeclaration(DeclnSpecs _specs, Declr _decl) {
            specs = _specs;

            if (_decl != null) {
                decl = _decl;
            } else {
                decl = new NullDeclarator();
            }
        }

        public readonly DeclnSpecs specs;
        public readonly Declr decl;

        // Get Parameter Declaration : env -> (env, name, type)
        // ====================================================
        // 
        public Tuple<AST.Env, string, AST.ExprType> GetParamDecln(AST.Env env) {
            Tuple<AST.Env, AST.Decln.SCS, AST.ExprType> r_specs = specs.GetSCSType(env);
            env = r_specs.Item1;
            AST.Decln.SCS scs = r_specs.Item2;
            AST.ExprType type = r_specs.Item3;

            Tuple<AST.Env, AST.ExprType, string> r_declr = decl.WrapExprType(env, type);
            env = r_declr.Item1;
            type = r_declr.Item2;
            string name = r_declr.Item3;

            return new Tuple<AST.Env, string, AST.ExprType>(env, name, type);
        }

    }


    // Initializer List
    // ================
    // used to initialize arrays and structs, etc
    // 
    // C language standard:
    // 1. scalar types
    //    
    // 2. aggregate types
    // 3. strings
    public class InitializerList : Expr {
        public InitializerList(List<Expr> _exprs) {
            initlist_exprs = _exprs;
        }
        public List<Expr> initlist_exprs;

        public override AST.Expr GetExpr(AST.Env env) {
            throw new InvalidOperationException();
        }

    }


    // Type Name
    // =========
    // describes a qualified type
    // 
    public class TypeName : PTNode {
        public TypeName(DeclnSpecs specs, Declr declr) {
            this.specs = specs;
            this.declr = declr;
        }

        public readonly DeclnSpecs specs;
        public readonly Declr declr;

        // TODO: check env
        public AST.ExprType GetExprType(AST.Env env) {
            AST.ExprType type = specs.GetExprType(env).Item2;
            return declr.WrapExprType(env, type).Item2;
        }

        [Obsolete]
        public Tuple<AST.Env, AST.ExprType> GetExprTypeEnv(AST.Env env) {
            Tuple<AST.Env, AST.ExprType> r_specs = specs.GetExprType(env);
            Tuple<AST.Env, AST.ExprType, string> r_declr = declr.WrapExprType(r_specs.Item1, r_specs.Item2);
            return Tuple.Create(r_declr.Item1, r_declr.Item2);
        }
    }

}