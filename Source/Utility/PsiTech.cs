/*
 *  Copyright 2019, 2020, K
 * 
 *  This file is part of PsiTech.
 *
 *  PsiTech is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Foobar is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with PsiTech. If not, see <https://www.gnu.org/licenses/>.
 *
 */

using UnityEngine;
using Verse;

namespace PsiTech.Utility {
    public class PsiTech : Mod {

        public PsiTech(ModContentPack content) : base(content) {
            GetSettings<PsiTechSettings>();
        }
        
        public override void DoSettingsWindowContents(Rect inRect) {
            base.DoSettingsWindowContents(inRect);
            GetSettings<PsiTechSettings>().DoWindowContents(inRect);
        }

        public override string SettingsCategory() {
            return "PsiTech";
        }
        
    }
}